using Microsoft.AspNetCore.Components;

namespace NED.WoT.BattleResults.Client.Models;
public enum Result
{
    Win,
    Lose,
    Draw,
    Unknown
}
public class BattleReport
{
    /// <summary>The one nl-NL culture of the UI: weekday names, decimal comma.</summary>
    public static readonly System.Globalization.CultureInfo DutchCulture = System.Globalization.CultureInfo.GetCultureInfo("nl-NL");

    public string? FileName { get; set; }
    public string? MapName { get; set; }
    public string? ReplayVersion { get; set; }
    public DateTime MatchStart { get; set; }
    public DateTime? MatchEnd
    {
        get
        {
            return MatchDuration.HasValue ? MatchStart.AddSeconds(MatchDuration.Value) : null;
        }
    }

    public string MatchTimeDisplay => $"{MatchStart:dd-MM-yyyy HH:mm}";

    /// <summary>The weekday and day, for the hover of the time cell.</summary>
    public string MatchDayDisplay
    {
        get
        {
            string day = MatchStart.ToString("dddd dd-MM-yyyy", DutchCulture);
            return $"{char.ToUpper(day[0], DutchCulture)}{day[1..]}";
        }
    }

    public int? MatchDuration { get; set; }
    public MarkupString MatchDurationDisplay
    {
        get
        {
            if (!MatchDuration.HasValue)
            {
                return (MarkupString)string.Empty;
            }

            TimeSpan time = TimeSpan.FromSeconds(MatchDuration.Value);
            return (MarkupString)$"{time.Minutes}m {time.Seconds}s";
        }
    }
    public Team Team1 { get; set; } = new Team();
    public Team Team2 { get; set; } = new Team();
    public Team? OwnTeam { get; set; }

    public Result Result { get; set; }
    public int? FinishReason { get; set; }
    public string? Error { get; internal set; }
    public bool ShowDetails { get; set; }

    /// <summary>The day the battle counts for: a battle with MatchStart.Hour &lt;= 2 counts for the previous day.</summary>
    public DateTime GroupDate => MatchStart.Hour <= 2 ? MatchStart.Date.AddDays(-1) : MatchStart.Date;

    public string Group
    {
        get
        {
            int daysAgo = (DateTime.Now.Date - GroupDate).Days;

            return daysAgo switch
            {
                0 => "Vandaag",
                1 => "Gisteren",
                _ => $"{daysAgo} dagen geleden",
            };
        }
    }
}

public class Team
{
    public int Number { get; set; }
    public string Base => Number == 1 ? "I" : "II";
    public string? Abbreviation { get; set; }
    public long? ClanId { get; set; }
    public string? ClanUrl => ClanId is > 0 ? $"https://eu.wargaming.net/clans/wot/{ClanId}/" : null;
    public int? Health { get; set; }
    public bool? IsWinner { get; set; }

    public bool IsOwnTeam { get; set; }

    public Result Result { get; set; }

    public string ResultDisplay
    {
        get
        {
            return Result switch
            {
                Result.Win => "Gewonnen",
                Result.Lose => "Verloren",
                Result.Draw => "Gelijkspel",
                _ => "Onbekend",
            };
        }
    }

    public List<Player> Players { get; set; } = [];

    public string GetResult(string? map)
    {
        List<string?> lines = [.. Players.Where(x => x.Name != null).OrderByDescending(x => x.ExperienceEarned).Select(x => x.Name)];
        lines.Insert(0, $"{map} {(Number == 1 ? "I" : "II")}");
        lines.Insert(1, ResultDisplay);

        return string.Join(Environment.NewLine, lines);
    }
}

public class Player(Team team)
{
    public Team Team { get; set; } = team;
    public int Number { get; set; }
    public string? Name { get; set; }
    public string? Clan { get; set; }
    public long? ClanId { get; set; }
    public string DisplayName => !string.IsNullOrEmpty(Clan) ? $"[{Clan}] {Name}" : $"{Name}";
    public string? Vehicle { get; set; }
    public int? MaxHealth { get; set; }
    public int? TeamNumber { get; set; }
    public bool? IsTeamKiller { get; set; }
    public int? Spotted { get; set; }
    public int? CapturePoints { get; set; }
    public int? DamageReceived { get; set; }
    public int? DamageBlocked { get; set; }
    public int? DamageDealt { get; set; }
    public int? Piercings { get; set; }
    public int? HitsReceived { get; set; }
    public int? ShotsReceived { get; set; }
    public int? ShotsBlocked { get; set; }
    public int? ExperienceEarned { get; set; }
    public int? CreditsEarned { get; set; }
    public int? Health { get; set; }
    public int? LifeTime { get; set; }
    public int? Kills { get; set; }
    public int? Shots { get; set; }
    public int? DirectHits { get; set; }
    public int? DeathReason { get; set; }
    public bool IsClanMember { get; set; }
}

