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
/// Wargaming sign-in (docs/PLAN.md section 3): browser login with a loopback redirect, token verified with
/// <c>account/info</c>, kept in <see cref="SecureStorage"/> only and prolonged at start. Wargaming answers HTTP 200 on
/// errors, so the envelope's <c>status</c> decides. The error's <c>value</c> echoes the token and is never read. Nothing here
/// logs, and no exception message carries a token. the logout endpoint is never called.
/// </summary>
public sealed class WargamingAuthService : IDisposable
{
    private const string StorageKey = "wargaming.session";
    private static readonly TimeSpan ProlongWithin = TimeSpan.FromHours(72);
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(14);
    private static readonly TimeSpan SignInTimeout = TimeSpan.FromMinutes(5);

    private readonly HttpClient _http;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);

    public WargamingAuthService(HttpClient http)
    {
        _http = http;
        SelfCheck();
    }

    /// <summary>The stored session, prolonged when it expires within 72 h. Null when none, expired or rejected (then cleared). Unreachable Wargaming keeps and returns the old token.</summary>
    public async Task<WargamingSession?> GetSessionAsync()
    {
        // One at a time: the stored session is re-read after the lock, so overlapping calls never both prolong (the old token dies at the first).
        await _sessionLock.WaitAsync();
        try
        {
            return await GetSessionCoreAsync();
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private async Task<WargamingSession?> GetSessionCoreAsync()
    {
        WargamingSession? session = await LoadAsync();
        if (session is null)
        {
            return null;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (session.ExpiresAt <= now)
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
            Reply reply = await PostAsync("auth/prolongate/", new Dictionary<string, string>
            {
                ["access_token"] = session.AccessToken,
                ["expires_at"] = now.Add(TokenLifetime).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            }, CancellationToken.None);

            if (reply.Ok)
            {
                JsonElement data = reply.Data;
                // The old token is dead from here on: store the new one before anything else.
                WargamingSession prolonged = session with
                {
                    AccessToken = data.GetProperty("access_token").GetString()!,
                    ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(data.GetProperty("expires_at").GetInt64()),
                };
                await StoreAsync(prolonged);
                return prolonged;
            }

            if (reply.ErrorMessage == "INVALID_ACCESS_TOKEN")
            {
                SignOut();
                return null;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            // Wargaming unreachable or an odd answer: keep the old token.
        }

        return session;
    }

    /// <summary>Opens the system browser, waits for the loopback callback, verifies the token and stores the session. Null when refused, timed out or cancelled.</summary>
    public async Task<WargamingSession?> SignInAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(SignInTimeout);

        int port = FreePort();
        string state = RandomNumberGenerator.GetHexString(32);
        string redirect = $"http://localhost:{port}/callback?state={state}";
        string loginUrl = $"{AppConfig.WargamingApiBaseUrl}auth/login/?application_id={AppConfig.WargamingApplicationId}"
            + $"&redirect_uri={Uri.EscapeDataString(redirect)}"
            + $"&expires_at={DateTimeOffset.UtcNow.Add(TokenLifetime).ToUnixTimeSeconds()}&nofollow=0";

        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        using CancellationTokenRegistration registration = timeout.Token.Register(listener.Close);

        try
        {
            await Browser.Default.OpenAsync(new Uri(loginUrl), BrowserLaunchMode.SystemPreferred);

            while (true)
            {
                HttpListenerContext context = await listener.GetContextAsync();
                if (context.Request.Url?.AbsolutePath != "/callback")
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    continue;
                }

                WargamingSession? session = null;
                try
                {
                    session = await VerifyCallbackAsync(context.Request.QueryString, state, timeout.Token);
                }
                catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or OperationCanceledException)
                {
                    // Treated as refused.
                }

                if (session is not null)
                {
                    await StoreAsync(session);
                }

                await RespondAsync(context, session is not null
                    ? "Je bent ingelogd. Je kunt dit venster sluiten."
                    : "Inloggen mislukt. Sluit dit venster en probeer het opnieuw.");
                return session;
            }
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>Forgets the token locally. Never calls Wargaming's logout endpoint.</summary>
#pragma warning disable CA1822 // instance member on purpose: DI consumers (#11, #12) call it on the service
    public void SignOut() => SecureStorage.Default.Remove(StorageKey);
#pragma warning restore CA1822

    private async Task<WargamingSession?> VerifyCallbackAsync(NameValueCollection query, string state, CancellationToken ct)
    {
        string? token = query["access_token"];
        if (!IsValidCallback(query["state"], state, query["status"], token) || !long.TryParse(query["account_id"], CultureInfo.InvariantCulture, out long accountId))
        {
            return null;
        }

        string id = accountId.ToString(CultureInfo.InvariantCulture);
        Reply reply = await PostAsync("account/info/", new Dictionary<string, string>
        {
            ["access_token"] = token!,
            ["account_id"] = id,
            // "private" is only present when the token belongs to this account.
            ["fields"] = "nickname,private",
        }, ct);

        if (!reply.Ok
            || reply.Data.ValueKind != JsonValueKind.Object
            || !reply.Data.TryGetProperty(id, out JsonElement info)
            || info.ValueKind != JsonValueKind.Object
            || !info.TryGetProperty("private", out JsonElement priv)
            || priv.ValueKind != JsonValueKind.Object
            || info.GetProperty("nickname").GetString() is not { Length: > 0 } nickname)
        {
            return null;
        }

        DateTimeOffset expires = long.TryParse(query["expires_at"], CultureInfo.InvariantCulture, out long unix)
            ? DateTimeOffset.FromUnixTimeSeconds(Math.Min(unix, DateTimeOffset.UtcNow.Add(TokenLifetime).ToUnixTimeSeconds()))
            : DateTimeOffset.UtcNow.Add(TokenLifetime);
        return new WargamingSession(accountId, nickname, token!, expires);
    }

    // POST form; only status and error.message are read, never the error's value (it echoes the token).
    private async Task<Reply> PostAsync(string endpoint, Dictionary<string, string> form, CancellationToken ct)
    {
        form["application_id"] = AppConfig.WargamingApplicationId;
        using var content = new FormUrlEncodedContent(form);
        using HttpResponseMessage response = await _http.PostAsync(new Uri(AppConfig.WargamingApiBaseUrl + endpoint), content, ct);
        using JsonDocument doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        JsonElement root = doc.RootElement;

        if (root.GetProperty("status").GetString() == "ok")
        {
            return new Reply(true, null, root.GetProperty("data").Clone());
        }

        string? message = root.TryGetProperty("error", out JsonElement error) && error.TryGetProperty("message", out JsonElement m) ? m.GetString() : null;
        return new Reply(false, message, default);
    }

    private static async Task<WargamingSession?> LoadAsync()
    {
        try
        {
            string? json = await SecureStorage.Default.GetAsync(StorageKey);
            return json is null ? null : JsonSerializer.Deserialize<WargamingSession>(json);
        }
        catch (Exception ex) when (ex is JsonException or CryptographicException)
        {
            // Unreadable (e.g. after a profile change): treat as signed out.
            return null;
        }
    }

    private static Task StoreAsync(WargamingSession session) => SecureStorage.Default.SetAsync(StorageKey, JsonSerializer.Serialize(session));

    private static async Task RespondAsync(HttpListenerContext context, string message)
    {
        byte[] body = Encoding.UTF8.GetBytes($"<!doctype html><meta charset=\"utf-8\"><title>WoT Gevechtslog</title><p>{WebUtility.HtmlEncode(message)}</p>");
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = body.Length;
        await context.Response.OutputStream.WriteAsync(body);
        context.Response.Close();
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    internal static bool NeedsProlong(DateTimeOffset expiresAt, DateTimeOffset now) => expiresAt - now <= ProlongWithin;

    internal static bool IsValidCallback(string? gotState, string expectedState, string? status, string? token) =>
        gotState == expectedState && status == "ok" && !string.IsNullOrEmpty(token);

    [Conditional("DEBUG")]
    private static void SelfCheck()
    {
        var now = new DateTimeOffset(2026, 3, 29, 2, 30, 0, TimeSpan.Zero);
        Debug.Assert(NeedsProlong(now.AddHours(71), now));
        Debug.Assert(NeedsProlong(now.AddHours(72), now));
        Debug.Assert(!NeedsProlong(now.AddHours(73), now));
        Debug.Assert(IsValidCallback("a", "a", "ok", "t"));
        Debug.Assert(!IsValidCallback("b", "a", "ok", "t"));
        Debug.Assert(!IsValidCallback("a", "a", "error", "t"));
        Debug.Assert(!IsValidCallback("a", "a", "ok", ""));
    }

    public void Dispose() => _sessionLock.Dispose();

    private readonly record struct Reply(bool Ok, string? ErrorMessage, JsonElement Data);
}
