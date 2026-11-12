using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;

using NED.WoT.BattleResults.Client.Models;

namespace NED.WoT.BattleResults.Client.Services;

/// <summary>What WoT HQ answered (docs/PLAN.md §3.3).</summary>
public abstract record SessionResult
{
    public sealed record Allowed(HqIdentity Identity) : SessionResult;

    public sealed record Refused(HqIdentity Identity) : SessionResult;

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
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

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
            using HttpResponseMessage response = await http.PostAsync(new Uri(AppConfig.HqBaseUrl + "api/gevechtslog/session"), form, timeout.Token);
            switch (response.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    return new SessionResult.InvalidToken();
                case HttpStatusCode.OK:
                    return Parse(await response.Content.ReadAsStringAsync(timeout.Token), session);
                default:
                    return new SessionResult.Unavailable();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            return new SessionResult.Unavailable();
        }
    }

    // Wargaming's spelling ("combat_officer") to the role; Unknown for null or a role we don't know.
    private static ClanRole ParseRole(string? role) =>
        role is not null && Enum.TryParse(role.Replace("_", ""), ignoreCase: true, out ClanRole parsed) && Enum.IsDefined(parsed) ? parsed : ClanRole.Unknown;

    // The clan fields are null for a player without a clan. HQ's nickname wins; the session's is the fallback.
    private static SessionResult Parse(string json, WargamingSession session)
    {
        HqAnswer a = JsonSerializer.Deserialize<HqAnswer>(json, Json) ?? throw new JsonException("Empty answer.");
        var identity = new HqIdentity(session.AccountId, a.Nickname ?? session.Nickname, a.ClanId, a.ClanTag, a.ClanName, a.EmblemUrl, ParseRole(a.Role), a.CanSeeAdvancedStats);
        return a.CanUse ? new SessionResult.Allowed(identity) : new SessionResult.Refused(identity);
    }

    // HQ's JSON contract (camelCase, docs/PLAN.md §3.3).
    private sealed record HqAnswer(string? Nickname, long? ClanId, string? ClanTag, string? ClanName, string? EmblemUrl, string? Role, bool CanUse, bool CanSeeAdvancedStats);

    [Conditional("DEBUG")]
    private static void SelfCheck()
    {
        var session = new WargamingSession(501742326, "Jim", "tok", DateTimeOffset.UnixEpoch);
        string allowed = """{"accountId":501742326,"nickname":"Jim","clanId":500012147,"clanTag":"NED-1","clanName":"N","emblemUrl":null,"role":"commander","canUse":true,"canSeeAdvancedStats":true}""";
        Debug.Assert(Parse(allowed, session) is SessionResult.Allowed { Identity: { ClanTag: "NED-1", Role: ClanRole.Commander, CanSeeAdvancedStats: true } });
        string refused = """{"accountId":501742326,"nickname":"Jim","clanId":null,"clanTag":null,"clanName":null,"emblemUrl":null,"role":null,"canUse":false,"canSeeAdvancedStats":false}""";
        Debug.Assert(Parse(refused, session) is SessionResult.Refused { Identity: { ClanId: null } });
    }
}
