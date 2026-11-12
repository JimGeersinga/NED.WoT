namespace NED.WoT.BattleResults.Client.Models;

/// <summary>A clan's public rating (<c>clanratings/clans</c> → <c>efficiency</c>). Rating and rank are null when the clan is not rated.</summary>
public sealed record ClanRating(string Tag, string Name, string? EmblemUrl, int? Rating, int? Rank);
