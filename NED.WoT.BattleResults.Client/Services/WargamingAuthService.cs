using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using NED.WoT.BattleResults.Client.Models;

namespace NED.WoT.BattleResults.Client.Services;

/// <summary>
/// Wargaming sign-in (docs/PLAN.md §3.2). The token lives only in <see cref="SecureStorage"/>: never logged, never in an
/// exception message. Wargaming answers HTTP 200 on errors, so the envelope's <c>status</c> decides; the error's
/// <c>value</c> echoes the token and is never read. The logout endpoint is never called.
/// </summary>
public sealed class WargamingAuthService(HttpClient http) : IDisposable
{
    private const string StorageKey = "wargaming.session";
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(14);
    private readonly SemaphoreSlim _lock = new(1, 1);

    static WargamingAuthService() => SelfCheck();

    /// <summary>The stored session, prolonged when it expires within 72 h. Null when none, expired or rejected (then cleared). Unreachable Wargaming keeps the old token.</summary>
    public async Task<WargamingSession?> GetSessionAsync()
    {
        // One at a time: the old token dies at the first prolong, so overlapping calls must not both prolong.
        await _lock.WaitAsync();
        try
        {
            WargamingSession? session;
            try
            {
                string? json = await SecureStorage.Default.GetAsync(StorageKey);
                session = json is null ? null : JsonSerializer.Deserialize<WargamingSession>(json);
            }
            catch (Exception ex) when (ex is JsonException or CryptographicException)
            {
                session = null; // unreadable, e.g. after a profile change
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (session is null || session.ExpiresAt <= now)
            {
                SignOut();
                return null;
            }

            if (!NeedsProlong(session.ExpiresAt, now))
            {
                return session;
            }

            try
            {
                (JsonElement? data, string? error) = await PostAsync("auth/prolongate/", new()
                {
                    ["access_token"] = session.AccessToken,
                    ["expires_at"] = (now + TokenLifetime).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                }, CancellationToken.None);

                if (data is { } d)
                {
                    session = session with
                    {
                        AccessToken = d.GetProperty("access_token").GetString()!,
                        ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(d.GetProperty("expires_at").GetInt64()),
                    };
                    await StoreAsync(session); // the old token is dead from here on
                }
                else if (error == "INVALID_ACCESS_TOKEN")
                {
                    SignOut();
                    return null;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
            {
                // Unreachable or an odd answer: keep the old token.
            }

            return session;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Opens the system browser, waits for the loopback callback, verifies the token and stores the session. Null when refused, after 5 minutes or on cancel.</summary>
    public async Task<WargamingSession?> SignInAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));

        // localhost, not 127.0.0.1: Wargaming's firewall blocks a redirect_uri with a literal IP.
        string prefix = $"http://localhost:{FreePort()}/";
        string state = RandomNumberGenerator.GetHexString(32);
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();
        using CancellationTokenRegistration stop = timeout.Token.Register(listener.Close);

        try
        {
            await Browser.Default.OpenAsync(new Uri($"{AppConfig.WargamingApiBaseUrl}auth/login/?application_id={AppConfig.WargamingApplicationId}"
                + $"&redirect_uri={Uri.EscapeDataString($"{prefix}callback?state={state}")}"
                + $"&expires_at={(DateTimeOffset.UtcNow + TokenLifetime).ToUnixTimeSeconds()}&nofollow=0"), BrowserLaunchMode.SystemPreferred);

            HttpListenerContext context = await listener.GetContextAsync();
            while (context.Request.Url?.AbsolutePath != "/callback") // e.g. a favicon request
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                context = await listener.GetContextAsync();
            }

            WargamingSession? session = null;
            try
            {
                session = await VerifyAsync(context.Request.QueryString, state, timeout.Token);
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException or OperationCanceledException)
            {
                // Refused.
            }

            if (session is not null)
            {
                await StoreAsync(session);
            }

            string message = session is null ? "Inloggen mislukt. Sluit dit venster en probeer het opnieuw." : "Je bent ingelogd. Je kunt dit venster sluiten.";
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Close(Encoding.UTF8.GetBytes($"<!doctype html><meta charset=\"utf-8\"><title>WoT Gevechtslog</title><p>{message}</p>"), willBlock: true);
            return session;
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>Forgets the token locally. Never calls Wargaming's logout endpoint.</summary>
    public static void SignOut() => SecureStorage.Default.Remove(StorageKey);

    public void Dispose() => _lock.Dispose();

    // The query values are not trusted: the token must pass account/info, and the nickname comes from that answer.
    private async Task<WargamingSession?> VerifyAsync(NameValueCollection query, string state, CancellationToken ct)
    {
        string? token = query["access_token"];
        if (!IsValidCallback(query["state"], state, query["status"], token)
            || !long.TryParse(query["account_id"], CultureInfo.InvariantCulture, out long accountId))
        {
            return null;
        }

        string id = accountId.ToString(CultureInfo.InvariantCulture);
        // "private" only comes back when the token belongs to this account.
        (JsonElement? data, _) = await PostAsync("account/info/", new() { ["access_token"] = token!, ["account_id"] = id, ["fields"] = "nickname,private" }, ct);
        if (data is not { } d)
        {
            return null;
        }

        JsonElement info = d.GetProperty(id);
        if (info.GetProperty("private").ValueKind != JsonValueKind.Object || info.GetProperty("nickname").GetString() is not { Length: > 0 } nickname)
        {
            return null;
        }

        DateTimeOffset max = DateTimeOffset.UtcNow + TokenLifetime;
        DateTimeOffset expires = long.TryParse(query["expires_at"], CultureInfo.InvariantCulture, out long unix) && unix < max.ToUnixTimeSeconds()
            ? DateTimeOffset.FromUnixTimeSeconds(unix)
            : max;
        return new WargamingSession(accountId, nickname, token!, expires);
    }

    // POST form. Returns data on status=ok, else error.message; never reads the error's value (it echoes the token).
    private async Task<(JsonElement? Data, string? Error)> PostAsync(string endpoint, Dictionary<string, string> form, CancellationToken ct)
    {
        form["application_id"] = AppConfig.WargamingApplicationId;
        using var content = new FormUrlEncodedContent(form);
        using HttpResponseMessage response = await http.PostAsync(new Uri(AppConfig.WargamingApiBaseUrl + endpoint), content, ct);
        using JsonDocument doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        JsonElement root = doc.RootElement;
        return root.GetProperty("status").GetString() == "ok"
            ? (root.GetProperty("data").Clone(), null)
            : (null, root.GetProperty("error").GetProperty("message").GetString());
    }

    private static Task StoreAsync(WargamingSession session) => SecureStorage.Default.SetAsync(StorageKey, JsonSerializer.Serialize(session));

    // HttpListener can't bind port 0, so ask the OS for a free one first.
    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static bool NeedsProlong(DateTimeOffset expiresAt, DateTimeOffset now) => expiresAt - now <= TimeSpan.FromHours(72);

    private static bool IsValidCallback(string? gotState, string state, string? status, string? token) =>
        gotState == state && status == "ok" && !string.IsNullOrEmpty(token);

    [Conditional("DEBUG")]
    private static void SelfCheck()
    {
        var now = new DateTimeOffset(2026, 3, 29, 2, 30, 0, TimeSpan.Zero);
        Debug.Assert(NeedsProlong(now.AddHours(72), now) && !NeedsProlong(now.AddHours(73), now));
        Debug.Assert(IsValidCallback("a", "a", "ok", "t"));
        Debug.Assert(!IsValidCallback("b", "a", "ok", "t") && !IsValidCallback("a", "a", "error", "t") && !IsValidCallback("a", "a", "ok", ""));
    }
}
