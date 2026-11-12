namespace NED.WoT.BattleResults.Client.Models;

/// <summary>A player's personal rating (<c>global_rating</c>). <see cref="PersonalRating"/> is null when the stats are hidden or the nickname is unknown. <see cref="Wtr"/> (<c>account/wtr</c> → <c>rating</c>) is null for an unknown id or an account without a WTR (account/wtr answers null for both), and when the WTR call failed. Hidden stats are <see cref="StatsHidden"/> (from account/info), not a null Wtr.</summary>
public sealed record PlayerRating(string Nickname, int? PersonalRating, bool StatsHidden, int? Wtr = null);
