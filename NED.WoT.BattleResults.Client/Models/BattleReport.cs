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

    public string MatchTimeDisplay => $"{MatchStart:dd/MM/yyyy HH:mm} - {MatchEnd?.ToString("HH:mm")}";

    public int? MatchDuration { get; set; }
    public MarkupString MatchDurationDisplay
    {
        get
        {
            string duration = string.Empty;
            if (MatchDuration.HasValue)
            {
                TimeSpan time = new(MatchDuration.Value * TimeSpan.TicksPerSecond);
                string minutes = time.Minutes.ToString();
                if (minutes.Length == 1)
                {
                    minutes = "&nbsp;&nbsp;" + minutes;
                }

                string seconds = time.Seconds.ToString();
                if (seconds.Length == 1)
                {
                    seconds = "&nbsp;&nbsp;" + seconds;
                }

                return (MarkupString)$"{minutes}m {seconds}s";
            }

            return (MarkupString)duration;
        }
    }
    public Team Team1 { get; set; } = new Team();
    public Team Team2 { get; set; } = new Team();
    public Team? OwnTeam { get; set; }

    public Result Result { get; set; }
    public int? FinishReason { get; set; }
    public string? Error { get; internal set; }
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
    public int? Health { get; set; }
    public bool? IsWinner { get; set; }

    public bool IsOwnTeam { get; set; }

    public Result Result { get; set; }

    public int HitsReceived => Players.Sum(x => x.HitsReceived ?? 0);
    public int ShotsReceived => Players.Sum(x => x.ShotsReceived ?? 0);
    public int DirectHits => Players.Sum(x => x.DirectHits ?? 0);
    public int Piercings => Players.Sum(x => x.Piercings ?? 0);

    public int HitRatio => HitsReceived == 0 ? 0 : (int)(100m / HitsReceived * DirectHits);
    public int PenRatio => DirectHits == 0 ? 0 : (int)(100m / DirectHits * Piercings);
    public int BlockRatio => ShotsReceived == 0 ? 0 : (int)(100m / ShotsReceived * (ShotsReceived - HitsReceived));

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
    public decimal ShotRatio => Shots.GetValueOrDefault() == 0 ? 0 : Shots.GetValueOrDefault() / (LifeTime.GetValueOrDefault() / 60m);
    public int HitRatio => HitsReceived.GetValueOrDefault() == 0 ? 0 : (int)(100m / Shots.GetValueOrDefault() * DirectHits.GetValueOrDefault());
    public int PenRatio => DirectHits.GetValueOrDefault() == 0 ? 0 : (int)(100m / DirectHits.GetValueOrDefault() * Piercings.GetValueOrDefault());
    public int BlockRatio => ShotsReceived.GetValueOrDefault() == 0 ? 0 : (int)(100m / ShotsReceived.GetValueOrDefault() * ShotsBlocked.GetValueOrDefault());
}

