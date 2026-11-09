using System.Globalization;
using System.Text.Json;

using NED.WoT.BattleResults.Client.Models;

namespace NED.WoT.BattleResults.Client.Services;

/// <summary>
/// Who the signed-in Wargaming player is: nickname from the session, clan from the public Clan API (no token, GET).
/// A failed clan lookup keeps the session and leaves the clan empty. Wargaming answers HTTP 200 on errors, so the
/// envelope's <c>status</c> decides and the error's <c>value</c> is never read.
/// </summary>
public sealed class IdentityService(WargamingAuthService auth, HttpClient http)
{
    private static readonly TimeSpan ClanLookupTimeout = TimeSpan.FromSeconds(5);

    public WargamingIdentity? Current { get; private set; }

    public event Action? Changed;

    /// <summary>Loads the stored session and the clan. False when there is no session.</summary>
    public async Task<bool> InitializeAsync() => await ApplyAsync(await auth.GetSessionAsync());

    /// <summary>Browser sign-in. False when refused, timed out or cancelled.</summary>
    public async Task<bool> SignInAsync(CancellationToken cancellationToken) => await ApplyAsync(await auth.SignInAsync(cancellationToken));

    public void SignOut()
    {
        auth.SignOut();
        Current = null;
        Changed?.Invoke();
    }

    private async Task<bool> ApplyAsync(WargamingSession? session)
    {
        if (session is null)
        {
            return false;
        }

        long? clanId = null;
        string? tag = null, name = null, emblem = null;
        using var timeout = new CancellationTokenSource(ClanLookupTimeout);
        try
        {
            string id = session.AccountId.ToString(CultureInfo.InvariantCulture);
            JsonElement? clan = await GetDataAsync($"clans/accountinfo/?application_id={AppConfig.WargamingApplicationId}&account_id={id}&fields=clan.clan_id,clan.tag,clan.name", id, timeout.Token);
            if (clan is { ValueKind: JsonValueKind.Object } info
                && info.TryGetProperty("clan", out JsonElement c) && c.ValueKind == JsonValueKind.Object)
            {
                clanId = c.GetProperty("clan_id").GetInt64();
                tag = c.GetProperty("tag").GetString();
                name = c.GetProperty("name").GetString();

                string cid = clanId.Value.ToString(CultureInfo.InvariantCulture);
                JsonElement? emblems = await GetDataAsync($"clans/info/?application_id={AppConfig.WargamingApplicationId}&clan_id={cid}&fields=emblems.x64", cid, timeout.Token);
                if (emblems is { ValueKind: JsonValueKind.Object } e
                    && e.TryGetProperty("emblems", out JsonElement em) && em.TryGetProperty("x64", out JsonElement x64)
                    && x64.TryGetProperty("portal", out JsonElement portal))
                {
                    emblem = portal.GetString();
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            // Wargaming unreachable or an odd answer: the session stays, the clan stays empty.
        }

        Current = new WargamingIdentity(session.AccountId, session.Nickname, clanId, tag, name, emblem);
        Changed?.Invoke();
        return true;
    }

    // data[key] of an ok envelope; null for an error status or a null entry.
    private async Task<JsonElement?> GetDataAsync(string relativeUrl, string key, CancellationToken ct)
    {
        using HttpResponseMessage response = await http.GetAsync(new Uri(AppConfig.WargamingApiBaseUrl + relativeUrl), ct);
        using JsonDocument doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        JsonElement root = doc.RootElement;
        if (root.GetProperty("status").GetString() != "ok"
            || !root.GetProperty("data").TryGetProperty(key, out JsonElement entry))
        {
            return null;
        }

        return entry.Clone();
    }
}
