using System.Diagnostics;

namespace NED.WoT.BattleResults.Client.Models;

/// <summary>WTR levels (docs/PLAN.md §3.4): 15 levels, a rating belongs to the highest limit it reaches.</summary>
public static class WtrLevel
{
    private static readonly int[] Limits = [0, 400, 1000, 1400, 1900, 2700, 3200, 3900, 4800, 5400, 6200, 7300, 8000, 8800, 9900];
    private static readonly string[] Names = ["Brons", "Zilver", "Goud", "Aas", "Legende"];
    private static readonly string[] Numerals = ["I", "II", "III"];

    static WtrLevel() => SelfCheck();

    /// <summary>The level index 0..14 for a rating; a negative rating counts as level 0.</summary>
    public static int Index(int wtr) => Math.Max(0, Array.FindLastIndex(Limits, limit => wtr >= limit));

    /// <summary>"Brons I" … "Legende III".</summary>
    public static string Name(int index) => $"{Names[index / 3]} {Numerals[index % 3]}";

    /// <summary>The icon, relative to wwwroot: <c>img/wtr/brons-i.png</c> … <c>legende-iii.png</c>, named after the level.</summary>
    public static string IconPath(int index) => $"img/wtr/{Name(index).ToLowerInvariant().Replace(' ', '-')}.png";

    [Conditional("DEBUG")]
    private static void SelfCheck()
    {
        Debug.Assert(Index(0) == 0 && Index(399) == 0 && Index(400) == 1 && Index(9899) == 13 && Index(9900) == 14 && Index(20000) == 14, "level boundaries");
        Debug.Assert(Name(Index(6180)) == "Aas I" && Name(Index(7340)) == "Aas III" && Name(Index(399)) == "Brons I" && Name(Index(9900)) == "Legende III", "level names");
        Debug.Assert(IconPath(Index(6180)) == "img/wtr/aas-i.png" && IconPath(Index(9900)) == "img/wtr/legende-iii.png", "icon path");
    }
}
