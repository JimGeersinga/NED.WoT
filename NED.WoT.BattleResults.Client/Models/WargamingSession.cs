namespace NED.WoT.BattleResults.Client.Models;

public sealed record WargamingSession(long AccountId, string Nickname, string AccessToken, DateTimeOffset ExpiresAt);
