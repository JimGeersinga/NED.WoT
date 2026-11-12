namespace NED.WoT.BattleResults.Client.Models;

/// <summary>A player's personal rating (<c>global_rating</c>). <see cref="PersonalRating"/> is null when the stats are hidden or the nickname is unknown.</summary>
public sealed record PlayerRating(string Nickname, int? PersonalRating, bool StatsHidden);
