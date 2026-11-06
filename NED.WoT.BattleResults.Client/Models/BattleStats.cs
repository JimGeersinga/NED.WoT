using System.Diagnostics;
using NED.WoT.BattleResults.Client.Services;

namespace NED.WoT.BattleResults.Client.Models;

/// <summary>The summed shooting figures of a set of players, and the ratios derived from them. The one place this maths lives (Index uses it for rows, day headers and the footer).</summary>
public record BattleStats(int? Shots, int? Hits, int? Pens, int? Blocked, int? Received, int? LifeTime)
{
    public static BattleStats Of(IEnumerable<Player> players)
    {
        var list = players as IList<Player> ?? players.ToList();
        return new BattleStats(
            list.Sum(x => x.Shots),
            list.Sum(x => x.DirectHits),
            list.Sum(x => x.Piercings),
            list.Sum(x => x.ShotsBlocked),
            list.Sum(x => x.ShotsReceived),
            list.Sum(x => x.LifeTime));
    }

    /// <summary>Shots per minute alive; 0 without shots or without time alive.</summary>
    public decimal ShotsPerMinute => Shots.GetValueOrDefault() == 0 || LifeTime.GetValueOrDefault() == 0 ? 0 : Shots.GetValueOrDefault() / (LifeTime.GetValueOrDefault() / 60m);

    /// <summary>Hits of the shots, in whole percent (rounded down).</summary>
    public int HitRatio => Percent(Hits, Shots);

    /// <summary>Penetrations of the hits, in whole percent (rounded down).</summary>
    public int PenRatio => Percent(Pens, Hits);

    /// <summary>Blocked of the shots received, in whole percent (rounded down).</summary>
    public int BlockRatio => Percent(Blocked, Received);

    private static int Percent(int? part, int? whole) => whole.GetValueOrDefault() == 0 ? 0 : (int)(100m * part.GetValueOrDefault() / whole.GetValueOrDefault());

    [Conditional("DEBUG")]
    public static void SelfCheck()
    {
        Debug.Assert(ColorHelper.GetHitLevel(50) == RatioLevel.Low && ColorHelper.GetHitLevel(51) == RatioLevel.Mid && ColorHelper.GetHitLevel(70) == RatioLevel.High, "hit: <= 50 low, 51 mid, >= 70 high");
        Debug.Assert(ColorHelper.GetPenLevel(55) == RatioLevel.Low && ColorHelper.GetPenLevel(56) == RatioLevel.Mid && ColorHelper.GetPenLevel(70) == RatioLevel.High, "pen: <= 55 low, 56 mid, >= 70 high");
        Debug.Assert(ColorHelper.GetBlockLevel(24) == RatioLevel.Mid && ColorHelper.GetBlockLevel(25) == RatioLevel.High, "block: 24 mid, >= 25 high");

        var team = new BattleStats(12, 3, 1, 2, 8, 600);
        Debug.Assert(team.ShotsPerMinute.ToString("0.0", BattleReport.DutchCulture) == "1,2", "12 shots in 600 s alive is 1,2 p/m");
        Debug.Assert(team.HitRatio == 25 && team.PenRatio == 33 && team.BlockRatio == 25, "25% hit, 33% pen, 25% block");
        Debug.Assert(new BattleStats(3, 3, 0, 0, 0, 0).HitRatio == 100 && new BattleStats(3, 1, 0, 0, 0, 0).HitRatio == 33, "100 / 3 * 3 is 100, not 99");
        Debug.Assert(new BattleStats(0, 0, 0, 0, 0, 0).ShotsPerMinute == 0 && new BattleStats(5, 0, 0, 0, 0, 0).ShotsPerMinute == 0, "no shots or no time alive is 0 p/m, no division by zero");
        Debug.Assert(new BattleStats(0, 0, 0, 0, 0, 0).HitRatio == 0, "no shots is 0%");

        var t = new Team();
        var players = new[] { new Player(t) { Shots = 4, DirectHits = 2, LifeTime = 60 }, new Player(t) { Shots = 6, DirectHits = 3, LifeTime = 120 } };
        Debug.Assert(Of(players) == new BattleStats(10, 5, 0, 0, 0, 180), "Of sums the players");
    }
}
