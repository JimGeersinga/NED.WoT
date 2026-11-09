namespace NED.WoT.BattleResults.Client.Models;

public sealed record WargamingIdentity(long AccountId, string Nickname, long? ClanId, string? ClanTag, string? ClanName, string? EmblemUrl);
