namespace NED.WoT.BattleResults.Client.Models;

/// <summary>The team name of one lobby. An unnamed lobby has no row.</summary>
public sealed class LobbyTeam
{
    public int Id { get; set; }

    /// <summary>"yyyy-MM-dd|prebattleID"; the day is <c>BattleReport.GroupDate</c>.</summary>
    public string LobbyKey { get; set; } = string.Empty;

    /// <summary>The name the user typed, already normalised (trimmed, at most 40 characters).</summary>
    public string TeamName { get; set; } = string.Empty;

    public DateTime ChangedAt { get; set; }
}
