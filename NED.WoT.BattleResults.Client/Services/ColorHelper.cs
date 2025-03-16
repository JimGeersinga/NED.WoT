namespace NED.WoT.BattleResults.Client.Services;

public static class ColorHelper
{
    public static string GetHitRatioColor(this int value) => value switch
    {
        <= 50 => "red",
        >= 70 => "green",
        _ => "orange"
    };

    public static string GetPenRatioColor(this int value) => value switch
    {
        <= 55 => "red",
        >= 70 => "green",
        _ => "orange"
    };

    public static string GetBlockedRatioColor(this int value) => value switch
    {
        >= 25 => "green",
        _ => "orange"
    };

    public static string GetWinRatioColor(this decimal value) => value switch
    {
        >= 50 => "Win",
        <= 40 => "Lose",
        _ => "Draw"
    };
}
