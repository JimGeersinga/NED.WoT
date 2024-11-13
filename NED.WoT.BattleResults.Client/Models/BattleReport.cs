using Microsoft.AspNetCore.Components;

namespace NED.WoT.BattleResults.Client.Models;


public enum Result
{
    Win,
    Lose,
    Draw,
    Unkown
}
public class BattleReport
{
    public string MapName { get; set; }
    public DateTime MatchStart { get; set; }
    public DateTime? MatchEnd
    {
        get
        {
            if (MatchDuration.HasValue)
            {
                return MatchStart.AddSeconds(MatchDuration.Value);
            }
            return null;
        }
    }
    public string ReplayVersion { get; set; }
    public int? MatchDuration { get; set; }
    public MarkupString MatchDurationDisplay
    {
        get
        {
            string duration = string.Empty;
            if (MatchDuration.HasValue)
            {
                TimeSpan time = new TimeSpan(MatchDuration.Value * TimeSpan.TicksPerSecond);
                string minutes = time.Minutes.ToString();
                if (minutes.Length == 1) minutes = "&nbsp;&nbsp;" + minutes;
                string seconds = time.Seconds.ToString();
                if (seconds.Length == 1) seconds = "&nbsp;&nbsp;" + seconds;

                return (MarkupString)$"{minutes}m {seconds}s";
            }
            return (MarkupString)duration;
        }
    }
    public Team Team1 { get; set; } = new Team();
    public Team Team2 { get; set; } = new Team();
    public int? FinishReason { get; set; }
    public string Error { get; internal set; }

    public string FileName { get; set; }

    public bool ShowDetails { get; set; }

    public string Group
    {
        get
        {
            int daysAgo = (DateTime.Now.Date - MatchStart.Date).Days;
            if (MatchStart.Hour <= 2)
            {
                daysAgo += 1;
            }

            if (daysAgo == 0)
            {
                return "Vandaag";
            }
            else if (daysAgo == 1)
            {
                return "Gisteren";
            }

            return $"{daysAgo} dagen geleden";
        }
    }

    public Team GetOwnTeam(Settings settings)
    {
        if (Team1.IsOwnTeam(settings)) return Team1;
        if (Team2.IsOwnTeam(settings)) return Team2;
        return default;
    }
    public bool IsDraw()
    {
        return Team1.IsWinner == false && Team2.IsWinner == false;
    }

    public bool IsUnkown()
    {
        return Team1.IsWinner == null && Team2.IsWinner == null;
    }

    public bool IsWin(Settings settings)
    {
        if (Team1.IsOwnTeam(settings))
        {
            return Team1.IsWinner == true;
        }
        else if (Team2.IsOwnTeam(settings))
        {
            return Team2.IsWinner == true;
        }
        return false;
    }

    public bool IsLose(Settings settings)
    {
        if (IsDraw())
        {
            return false;
        }

        if (Team1.IsOwnTeam(settings))
        {
            return Team1.IsWinner == false;
        }
        else if (Team2.IsOwnTeam(settings))
        {
            return Team2.IsWinner == false;
        }
        return false;
    }
}


public class Team
{
    public int Number { get; set; }
    public string Abbreviation { get; set; }
    public int? Health { get; set; }
    public bool? IsWinner { get; set; }
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

    public bool IsOwnTeam(Settings settings)
    {
        return Abbreviation?.ToLower() == settings.ClanAbbreviation?.ToLower() || Players.Any(x => x.Name?.ToLower() == settings.PlayerName?.ToLower());
    }

    public string GetResult(string map)
    {
        List<string> lines = Players.Where(x => x.Name != null).OrderByDescending(x => x.ExperienceEarned).Select(x => x.Name).ToList();
        lines.Insert(0, $"{map} {(Number == 1 ? "I" : "II")}");
        lines.Insert(1, ResultDisplay);

        return string.Join(Environment.NewLine, lines);
    }
}

public class Player
{
    public int Number { get; set; }
    public string Name { get; set; }
    public string Clan { get; set; }
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(Clan))
            {
                return $"[{Clan}] {Name}";
            }
            return $"{Name}";
        }
    }
    public string Vehicle { get; set; }
    public int? MaxHealth { get; set; }
    public bool? IsTeamKiller { get; set; }
    public int? Spotted { get; set; }
    public int? CapturePoints { get; set; }
    public int? DamageReceived { get; set; }
    public int? DamageBlocked { get; set; }
    public int? DamageDealt { get; set; }
    public int? Piercings { get; set; }
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

