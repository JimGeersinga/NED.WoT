namespace NED.WoT.BattleResults.Client.Models;

/// <summary>Who HQ says the player is: clan and rights come from WoT HQ (docs/PLAN.md §3.3). <paramref name="Role"/> is Wargaming's spelling, e.g. <c>commander</c>.</summary>
public sealed record WargamingIdentity(long AccountId, string Nickname, long? ClanId, string? ClanTag, string? ClanName, string? EmblemUrl, string? Role, bool CanUse, bool CanSeeAdvancedStats);
