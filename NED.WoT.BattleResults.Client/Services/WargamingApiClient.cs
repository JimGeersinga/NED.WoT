using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

using NED.WoT.BattleResults.Client.Models;

namespace NED.WoT.BattleResults.Client.Services;

/// <summary>Result of <see cref="WargamingApiClient.GetPlayerRatingsAsync"/>. <see cref="Players"/> is empty when <see cref="Ok"/> is false.</summary>
public sealed record RatingsResult(bool Ok, IReadOnlyList<PlayerRating> Players);

/// <summary>Result of <see cref="WargamingApiClient.GetClanRatingAsync"/>. <see cref="Clan"/> is null when failed or when no clan has that tag.</summary>
public sealed record ClanRatingResult(bool Ok, ClanRating? Clan);

/// <summary>
/// Wargaming public reads (docs/PLAN.md §3.4): GET, no token, under the app's own application id. Wargaming answers HTTP 200
/// on errors, so the envelope's <c>status</c> decides and the error's <c>value</c> is never read. Nothing here throws: a
/// failure is a result with <c>Ok = false</c>.
/// </summary>
public sealed class WargamingApiClient(HttpClient http)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private const int ListLimit = 100; // account/list: max 100 per call

    static WargamingApiClient() => SelfCheck();

    /// <summary>Personal rating per nickname (exact, case-insensitive match). Unknown nicknames come back with a null rating.</summary>
    public async Task<RatingsResult> GetPlayerRatingsAsync(IReadOnlyCollection<string> nicknames, CancellationToken cancellationToken)
    {
        try
        {
            var result = new List<PlayerRating>();
            foreach (string[] chunk in nicknames.Distinct(StringComparer.OrdinalIgnoreCase).Chunk(ListLimit))
            {
                using JsonDocument? list = await GetAsync("account/list/", $"search={Uri.EscapeDataString(string.Join(',', chunk))}&type=exact&limit={ListLimit}", cancellationToken);
                if (list is null)
                {
                    return new(false, []);
                }

                var ids = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                foreach (JsonElement a in list.RootElement.GetProperty("data").EnumerateArray())
                {
                    ids[a.GetProperty("nickname").GetString()!] = a.GetProperty("account_id").GetInt64();
                }

                using JsonDocument? info = ids.Count == 0 ? null : await GetAsync("account/info/", $"account_id={string.Join(',', ids.Values)}&fields=global_rating", cancellationToken);
                if (ids.Count > 0 && info is null)
                {
                    return new(false, []);
                }

                foreach (string name in chunk)
                {
                    if (!ids.TryGetValue(name, out long id))
                    {
                        result.Add(new(name, null, false));
                        continue;
                    }

                    // data[id] is null when the player hides their stats.
                    JsonElement entry = info!.RootElement.GetProperty("data").GetProperty(id.ToString(CultureInfo.InvariantCulture));
                    int? pr = entry.ValueKind == JsonValueKind.Object ? entry.GetProperty("global_rating").GetInt32() : null;
                    result.Add(new(name, pr, pr is null));
                }
            }

            return new(true, result);
        }
        catch (Exception ex) when (IsFailure(ex, cancellationToken))
        {
            return new(false, []);
        }
    }

    /// <summary>The clan with exactly this tag (clans/list is a prefix search) and its efficiency rating and rank.</summary>
    public async Task<ClanRatingResult> GetClanRatingAsync(string tag, CancellationToken cancellationToken)
    {
        try
        {
            using JsonDocument? list = await GetAsync("clans/list/", $"search={Uri.EscapeDataString(tag)}&fields=clan_id,tag,name,emblems.x64", cancellationToken);
            if (list is null)
            {
                return new(false, null);
            }

            JsonElement? clan = null;
            foreach (JsonElement c in list.RootElement.GetProperty("data").EnumerateArray())
            {
                if (string.Equals(c.GetProperty("tag").GetString(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    clan = c;
                    break;
                }
            }

            if (clan is not { } found)
            {
                return new(true, null);
            }

            long id = found.GetProperty("clan_id").GetInt64();
            using JsonDocument? rating = await GetAsync("clanratings/clans/", $"clan_id={id}&fields=efficiency", cancellationToken);
            if (rating is null)
            {
                return new(false, null);
            }

            int? value = null, rank = null;
            JsonElement entry = rating.RootElement.GetProperty("data").GetProperty(id.ToString(CultureInfo.InvariantCulture));
            if (entry.ValueKind == JsonValueKind.Object && entry.GetProperty("efficiency") is { ValueKind: JsonValueKind.Object } eff)
            {
                value = eff.GetProperty("value").GetInt32();
                rank = eff.GetProperty("rank").ValueKind == JsonValueKind.Null ? null : eff.GetProperty("rank").GetInt32();
            }

            string? emblem = found.TryGetProperty("emblems", out JsonElement e) && e.ValueKind == JsonValueKind.Object
                ? e.GetProperty("x64").GetProperty("portal").GetString()
                : null;
            return new(true, new ClanRating(found.GetProperty("tag").GetString()!, found.GetProperty("name").GetString()!, emblem, value, rank));
        }
        catch (Exception ex) when (IsFailure(ex, cancellationToken))
        {
            return new(false, null);
        }
    }

    // 10 s timeout and one retry after 1 s on REQUEST_LIMIT_EXCEEDED. Null on any Wargaming error; the caller owns the document.
    private async Task<JsonDocument?> GetAsync(string endpoint, string query, CancellationToken ct)
    {
        for (int attempt = 0; ; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);
            using HttpResponseMessage response = await http.GetAsync(new Uri($"{AppConfig.WargamingApiBaseUrl}{endpoint}?application_id={AppConfig.WargamingApplicationId}&{query}"), timeout.Token);
            JsonDocument doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
            JsonElement root = doc.RootElement;
            if (root.GetProperty("status").GetString() == "ok")
            {
                return doc;
            }

            string? message = root.GetProperty("error").GetProperty("message").GetString(); // never error.value
            doc.Dispose();
            if (message != "REQUEST_LIMIT_EXCEEDED" || attempt > 0)
            {
                return null;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
    }

    // Caller cancellation is not swallowed.
    private static bool IsFailure(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException
        || (ex is OperationCanceledException && !ct.IsCancellationRequested);

    [Conditional("DEBUG")]
    private static void SelfCheck()
    {
        // Offline: an unreachable proxy must give failed results, not exceptions, well inside 10 s.
        _ = Task.Run(async () =>
        {
            using var offline = new HttpClient(new HttpClientHandler { Proxy = new System.Net.WebProxy("http://127.0.0.1:9"), UseProxy = true });
            var client = new WargamingApiClient(offline);
            var sw = Stopwatch.StartNew();
            RatingsResult r = await client.GetPlayerRatingsAsync(["barryone"], CancellationToken.None);
            ClanRatingResult c = await client.GetClanRatingAsync("NED-1", CancellationToken.None);
            Debug.Assert(!r.Ok && !c.Ok && sw.Elapsed < TimeSpan.FromSeconds(11));
        });
    }
}
