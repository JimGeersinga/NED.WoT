namespace NED.WoT.BattleResults.Client.Models;

/// <summary>Who HQ says the player is: clan and rights come from WoT HQ (docs/PLAN.md §3.3). <paramref name="Role"/> is <see cref="ClanRole.Unknown"/> without a clan.</summary>
public sealed record HqIdentity(long AccountId, string Nickname, long? ClanId, string? ClanTag, string? ClanName, string? EmblemUrl, ClanRole Role, bool CanSeeAdvancedStats);
