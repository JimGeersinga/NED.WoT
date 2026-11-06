namespace NED.WoT.BattleResults.Client.Services;

public enum RatioLevel { Low, Mid, High }

public static class ColorHelper
{
    public static RatioLevel GetHitLevel(this int value) => value switch
    {
        <= 50 => RatioLevel.Low,
        >= 70 => RatioLevel.High,
        _ => RatioLevel.Mid
    };

    public static RatioLevel GetPenLevel(this int value) => value switch
    {
        <= 55 => RatioLevel.Low,
        >= 70 => RatioLevel.High,
        _ => RatioLevel.Mid
    };

    /// <summary>Block has no Low: below 25 is Mid.</summary>
    public static RatioLevel GetBlockLevel(this int value) => value >= 25 ? RatioLevel.High : RatioLevel.Mid;

    /// <summary>The palette colour of a level: Low error, Mid warning, High success.</summary>
    public static string GetColor(this RatioLevel level) => level switch
    {
        RatioLevel.Low => "var(--mud-palette-error)",
        RatioLevel.High => "var(--mud-palette-success)",
        _ => "var(--mud-palette-warning)"
    };

    public static string GetHitRatioColor(this int value) => value.GetHitLevel().GetColor();

    public static string GetPenRatioColor(this int value) => value.GetPenLevel().GetColor();

    public static string GetBlockedRatioColor(this int value) => value.GetBlockLevel().GetColor();

    public static string GetWinRatioColor(this decimal value) => value switch
    {
        >= 50 => "Win",
        <= 40 => "Lose",
        _ => "Draw"
    };
}
