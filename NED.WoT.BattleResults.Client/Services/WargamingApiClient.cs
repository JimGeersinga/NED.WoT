using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

using NED.WoT.BattleResults.Client.Models;

namespace NED.WoT.BattleResults.Client.Services;

/// <summary>Result of <see cref="WargamingApiClient.GetPlayerRatingsAsync"/>. <see cref="Players"/> is empty when <see cref="Ok"/> is false.</summary>
public sealed record RatingsResult(bool Ok, IReadOnlyList<PlayerRating> Players);

/// <summary>Result of <see cref="WargamingApiClient.GetClanRatingAsync"/>. <see cref="Clan"/> is null when failed or when no clan has that tag.</summary>
public sealed record ClanRatingResult(bool Ok, ClanRating? Clan);

/// <summary>
/// Wargaming public reads (docs/PLAN.md §3.4): GET, no token, under the app's own application id. Wargaming answers HTTP 200
/// on errors, so the envelope's <c>status</c> decides. <see cref="WgError"/> has no <c>Value</c> member, so the error's
/// <c>value</c> is never materialised. Nothing here throws except caller cancellation: a failure is a result with <c>Ok = false</c>.
/// </summary>
public sealed partial class WargamingApiClient(HttpClient http, ILogger<WargamingApiClient> logger)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private const int ListLimit = 100; // account/list: max 100 per call

    static WargamingApiClient() => SelfCheck();

    private sealed record Envelope(string? Status, WgError? Error, JsonElement Data);
    private sealed record WgError(string? Message);
    private sealed record Account(long AccountId, string Nickname);
    private sealed record AccountInfo(int? GlobalRating);
    private sealed record AccountWtr(int? Rating);
    private sealed record Clan(long ClanId, string Tag, string Name, Emblems? Emblems);
    private sealed record Emblems([property: JsonPropertyName("x64")] EmblemSet? X64);
    private sealed record EmblemSet(string? Portal);
    private sealed record ClanEntry(Efficiency? Efficiency);
    private sealed record Efficiency(int? Value, int? Rank);

    /// <summary>
    /// Personal rating per nickname: one row per distinct nickname (trimmed, case-insensitive; blanks dropped), in input order.
    /// A nickname Wargaming does not know has a null rating and StatsHidden false; a known one whose <c>data[id]</c> is null
    /// has StatsHidden true.
    /// </summary>
    public async Task<RatingsResult> GetPlayerRatingsAsync(IReadOnlyCollection<string> nicknames, CancellationToken cancellationToken)
    {
        // One 10 s budget for the whole call, retries included.
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(Timeout);
        CancellationToken ct = budget.Token;
        string endpoint = "account/list";
        try
        {
            var result = new List<PlayerRating>();
            IEnumerable<string> names = nicknames.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (string[] chunk in names.Chunk(ListLimit))
            {
                List<Account>? accounts = await GetAsync<List<Account>>(endpoint, $"search={Uri.EscapeDataString(string.Join(',', chunk))}&type=exact&limit={ListLimit}", ct);
                if (accounts is null)
                {
                    return new(false, []);
                }

                Dictionary<string, AccountInfo?>? info = [];
                Dictionary<string, AccountWtr?>? wtr = [];
                if (accounts.Count > 0)
                {
                    endpoint = "account/info";
                    info = await GetAsync<Dictionary<string, AccountInfo?>>(endpoint, $"account_id={string.Join(',', accounts.Select(a => a.AccountId))}&fields=global_rating", ct);
                    if (info is null)
                    {
                        return new(false, []);
                    }

                    wtr = await GetWtrAsync(accounts, ct);
                }

                result.AddRange(MapPlayers(chunk, accounts, info, wtr));
            }

            return new(true, result);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
        {
            string type = ex.GetType().Name;
            LogFailed(endpoint, type);
            return new(false, []);
        }
    }

    /// <summary>The clan with exactly this tag (clans/list is a prefix search) and its efficiency rating and rank.</summary>
    public async Task<ClanRatingResult> GetClanRatingAsync(string tag, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(Timeout);
        CancellationToken ct = budget.Token;
        string endpoint = "clans/list";
        try
        {
            // ponytail: one page of 100 prefix matches; an exact tag could fall off it for a very short, common tag. Page on if that bites.
            List<Clan>? clans = await GetAsync<List<Clan>>(endpoint, $"search={Uri.EscapeDataString(tag)}&limit=100&fields=clan_id,tag,name,emblems.x64", ct);
            if (clans is null)
            {
                return new(false, null);
            }

            if (PickClan(clans, tag) is not { } clan)
            {
                return new(true, null);
            }

            endpoint = "clanratings/clans";
            Dictionary<string, ClanEntry?>? ratings = await GetAsync<Dictionary<string, ClanEntry?>>(endpoint, $"clan_id={clan.ClanId}&fields=efficiency", ct);
            return ratings is null ? new(false, null) : new(true, MapClan(clan, ratings));
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
        {
            string type = ex.GetType().Name;
            LogFailed(endpoint, type);
            return new(false, null);
        }
    }

    // WTR is a bonus: any failure of this call (Wargaming error, bad JSON, network) is logged and gives no WTR for the chunk, while PR
    // still comes back. Caller cancellation and the 10 s budget are not swallowed: they still end the whole call as a failed result.
    private async Task<Dictionary<string, AccountWtr?>> GetWtrAsync(List<Account> accounts, CancellationToken ct)
    {
        try
        {
            return await GetAsync<Dictionary<string, AccountWtr?>>("account/wtr", $"account_id={string.Join(',', accounts.Select(a => a.AccountId))}", ct) ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            string type = ex.GetType().Name;
            LogFailed("account/wtr", type);
            return [];
        }
    }

    // Null on a Wargaming error (logged). One retry after 1 s on REQUEST_LIMIT_EXCEEDED, inside the caller's budget.
    private async Task<T?> GetAsync<T>(string endpoint, string query, CancellationToken ct)
        where T : class
    {
        for (int attempt = 0; ; attempt++)
        {
            using HttpResponseMessage response = await http.GetAsync(new Uri($"{AppConfig.WargamingApiBaseUrl}{endpoint}/?application_id={AppConfig.WargamingApplicationId}&{query}"), ct);
            (T? data, string? error) = Parse<T>(await response.Content.ReadAsStringAsync(ct));
            if (error is null)
            {
                return data;
            }

            if (error != "REQUEST_LIMIT_EXCEEDED" || attempt > 0)
            {
                LogFailed(endpoint, "WargamingError");
                return null;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
    }

    // Two steps: the envelope first, so a failed call never touches data. Error is null on status ok (then Data is the payload,
    // null when the payload is JSON null); else the error message, or "unknown".
    private static (T? Data, string? Error) Parse<T>(string json)
        where T : class
    {
        Envelope env = JsonSerializer.Deserialize<Envelope>(json, Json) ?? throw new JsonException("empty envelope");
        return env.Status == "ok"
            ? (env.Data.Deserialize<T>(Json), null)
            : (null, env.Error?.Message ?? "unknown");
    }

    private static List<PlayerRating> MapPlayers(string[] names, List<Account> accounts, Dictionary<string, AccountInfo?> info, Dictionary<string, AccountWtr?> wtr)
    {
        var ids = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (Account a in accounts)
        {
            ids[a.Nickname] = a.AccountId;
        }

        var rows = new List<PlayerRating>();
        foreach (string name in names)
        {
            if (!ids.TryGetValue(name, out long id))
            {
                rows.Add(new(name, null, false)); // unknown
                continue;
            }

            // A missing key, or an object without global_rating, is an odd answer: fail the call. Only a null entry is hidden.
            if (!info.TryGetValue(id.ToString(CultureInfo.InvariantCulture), out AccountInfo? entry))
            {
                throw new JsonException("account/info lacks an account");
            }

            // WTR is tolerant: a null entry, a missing key or an object without a rating all mean no WTR.
            int? wtrRating = wtr.TryGetValue(id.ToString(CultureInfo.InvariantCulture), out AccountWtr? w) ? w?.Rating : null;
            rows.Add(entry is null
                ? new(name, null, true, wtrRating)
                : new(name, entry.GlobalRating ?? throw new JsonException("account/info entry lacks global_rating"), false, wtrRating));
        }

        return rows;
    }

    private static Clan? PickClan(List<Clan> clans, string tag) =>
        clans.FirstOrDefault(c => string.Equals(c.Tag, tag, StringComparison.OrdinalIgnoreCase));

    // Tolerant: an unrated clan, or a missing efficiency, value, rank or emblem, gives nulls.
    private static ClanRating MapClan(Clan clan, Dictionary<string, ClanEntry?> ratings)
    {
        ratings.TryGetValue(clan.ClanId.ToString(CultureInfo.InvariantCulture), out ClanEntry? entry);
        return new(clan.ClanId, clan.Tag, clan.Name, clan.Emblems?.X64?.Portal, entry?.Efficiency?.Value, entry?.Efficiency?.Rank);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Wargaming public read failed: {Endpoint}: {Type}")]
    private partial void LogFailed(string endpoint, string type);

    [Conditional("DEBUG")]
    private static void SelfCheck()
    {
        // Pure, on canned JSON: no network.
        var accounts = Parse<List<Account>>("""{"status":"ok","data":[{"nickname":"BarryOne","account_id":1},{"nickname":"Hidey","account_id":2}]}""").Data!;
        var info = Parse<Dictionary<string, AccountInfo?>>("""{"status":"ok","data":{"1":{"global_rating":4930},"2":null}}""").Data!;
        var wtrs = Parse<Dictionary<string, AccountWtr?>>("""{"status":"ok","data":{"1":{"rating":5333,"account_id":1},"2":null}}""").Data!;
        List<PlayerRating> rows = MapPlayers(["barryone", "Hidey", "Nobody"], accounts, info, wtrs);
        Debug.Assert(rows[0] == new PlayerRating("barryone", 4930, false, 5333), "case-insensitive match, with WTR");
        Debug.Assert(rows[1] == new PlayerRating("Hidey", null, true), "null data[id] is hidden, null WTR is none");
        Debug.Assert(rows[2] == new PlayerRating("Nobody", null, false), "unknown is not hidden");

        bool failed = false;
        try
        {
            MapPlayers(["barryone"], accounts, Parse<Dictionary<string, AccountInfo?>>("""{"status":"ok","data":{"1":{}}}""").Data!, wtrs);
        }
        catch (JsonException)
        {
            failed = true;
        }

        Debug.Assert(failed, "an object without global_rating fails the call");

        Debug.Assert(MapPlayers(["barryone"], accounts, info, Parse<Dictionary<string, AccountWtr?>>("""{"status":"ok","data":{"1":{}}}""").Data!)[0].Wtr is null, "an object without a WTR rating is no WTR");
        Debug.Assert(MapPlayers(["barryone"], accounts, info, [])[0] == new PlayerRating("barryone", 4930, false), "a missing WTR key (or a failed WTR call) is no WTR, PR stays");

        var clans = Parse<List<Clan>>("""{"status":"ok","data":[{"clan_id":9,"tag":"NED-10","name":"Other"},{"clan_id":5,"tag":"NED-1","name":"Noobs","emblems":{"x64":{"portal":"u"}}}]}""").Data!;
        Clan clan = PickClan(clans, "ned-1")!;
        Debug.Assert(clan.ClanId == 5 && PickClan(clans, "NED") is null, "exact tag on a prefix result");
        Debug.Assert(MapClan(clan, Parse<Dictionary<string, ClanEntry?>>("""{"status":"ok","data":{"5":{"efficiency":{"value":7261,"rank":643}}}}""").Data!) == new ClanRating(5, "NED-1", "Noobs", "u", 7261, 643));
        string[] odd =
        [
            """{"status":"ok","data":{"5":null}}""",
            """{"status":"ok","data":{"5":{}}}""",
            """{"status":"ok","data":{"5":{"efficiency":null}}}""",
            """{"status":"ok","data":{"5":{"efficiency":{"value":null,"rank":null}}}}""",
        ];
        foreach (string json in odd)
        {
            Debug.Assert(MapClan(clan, Parse<Dictionary<string, ClanEntry?>>(json).Data!) == new ClanRating(5, "NED-1", "Noobs", "u", null, null), json);
        }

        Debug.Assert(MapClan(new(5, "T", "N", new(null)), []) == new ClanRating(5, "T", "N", null, null, null), "no emblem, no entry");

        (List<Account>? data, string? error) = Parse<List<Account>>("""{"status":"error","error":{"code":407,"message":"REQUEST_LIMIT_EXCEEDED","field":null,"value":"SECRET"}}""");
        Debug.Assert(data is null && error == "REQUEST_LIMIT_EXCEEDED", "error envelope gives the message");
        Debug.Assert(typeof(WgError).GetProperty("Value") is null, "error.value is never materialised");
    }
}
