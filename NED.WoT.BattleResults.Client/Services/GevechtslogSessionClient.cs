using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;

using NED.WoT.BattleResults.Client.Models;

namespace NED.WoT.BattleResults.Client.Services;

/// <summary>What WoT HQ answered (docs/PLAN.md §3.3).</summary>
public abstract record SessionResult
{
    public sealed record Allowed(WargamingIdentity Identity) : SessionResult;

    public sealed record Refused(WargamingIdentity Identity) : SessionResult;

    /// <summary>HTTP 401: the token is no good, sign in again.</summary>
    public sealed record InvalidToken : SessionResult;

    /// <summary>503, any other status, a bad body, a timeout or no connection: no access.</summary>
    public sealed record Unavailable : SessionResult;
}

/// <summary>
/// Asks HQ whether the signed-in account may use the app. The token goes in a POST form, never in a URL; it is never
/// logged, and the error body is never read (it can echo the request).
/// </summary>
public sealed class GevechtslogSessionClient(HttpClient http)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    static GevechtslogSessionClient() => SelfCheck();

    public async Task<SessionResult> StartAsync(WargamingSession session, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["account_id"] = session.AccountId.ToString(CultureInfo.InvariantCulture),
                ["access_token"] = session.AccessToken,
            });
            using HttpResponseMessage response = await http.PostAsync(new Uri(AppConfig.ClanApiBaseUrl + "api/gevechtslog/session"), form, timeout.Token);
            switch (response.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    return new SessionResult.InvalidToken();
                case HttpStatusCode.OK:
                    using (JsonDocument doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token))
                    {
                        return Parse(doc.RootElement, session);
                    }
                default:
                    return new SessionResult.Unavailable();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return new SessionResult.Unavailable();
        }
    }

    // The clan fields are null for a player without a clan. HQ's nickname wins; the session's is the fallback.
    private static SessionResult Parse(JsonElement root, WargamingSession session)
    {
        var identity = new WargamingIdentity(
            session.AccountId,
            Str(root, "nickname") ?? session.Nickname,
            root.GetProperty("clanId").ValueKind == JsonValueKind.Number ? root.GetProperty("clanId").GetInt64() : null,
            Str(root, "clanTag"),
            Str(root, "clanName"),
            Str(root, "emblemUrl"),
            Str(root, "role"),
            root.GetProperty("canUse").GetBoolean(),
            root.GetProperty("canSeeAdvancedStats").GetBoolean());
        return identity.CanUse ? new SessionResult.Allowed(identity) : new SessionResult.Refused(identity);
    }

    private static string? Str(JsonElement e, string name) => e.GetProperty(name).GetString();

    [Conditional("DEBUG")]
    private static void SelfCheck()
    {
        var session = new WargamingSession(501742326, "Jim", "tok", DateTimeOffset.UnixEpoch);
        using JsonDocument allowed = JsonDocument.Parse("""{"accountId":501742326,"nickname":"Jim","clanId":500012147,"clanTag":"NED-1","clanName":"N","emblemUrl":null,"role":"commander","canUse":true,"canSeeAdvancedStats":true}""");
        Debug.Assert(Parse(allowed.RootElement, session) is SessionResult.Allowed { Identity: { ClanTag: "NED-1", Role: "commander", CanSeeAdvancedStats: true } });
        using JsonDocument refused = JsonDocument.Parse("""{"accountId":501742326,"nickname":"Jim","clanId":null,"clanTag":null,"clanName":null,"emblemUrl":null,"role":null,"canUse":false,"canSeeAdvancedStats":false}""");
        Debug.Assert(Parse(refused.RootElement, session) is SessionResult.Refused { Identity: { ClanId: null, CanUse: false } });
    }
}
