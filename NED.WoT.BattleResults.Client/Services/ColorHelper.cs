namespace NED.WoT.BattleResults.Client.Services;

public static class ColorHelper
{
    public static string GetHitRatioColor(this int value) => value switch
    {
        <= 50 => "var(--mud-palette-error)",
        >= 70 => "var(--mud-palette-success)",
        _ => "var(--mud-palette-warning)"
    };

    public static string GetPenRatioColor(this int value) => value switch
    {
        <= 55 => "var(--mud-palette-error)",
        >= 70 => "var(--mud-palette-success)",
        _ => "var(--mud-palette-warning)"
    };

    public static string GetBlockedRatioColor(this int value) => value switch
    {
        >= 25 => "var(--mud-palette-success)",
        _ => "var(--mud-palette-warning)"
    };

    public static string GetWinRatioColor(this decimal value) => value switch
    {
        >= 50 => "Win",
        <= 40 => "Lose",
        _ => "Draw"
    };
}
