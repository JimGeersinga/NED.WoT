using System.Globalization;

namespace NED.WoT.BattleResults.Client.Components.Battles;

internal static class NumberFormat
{
    // U+2009 thin space as the thousands separator.
    private static readonly NumberFormatInfo ThinSpace = new() { NumberGroupSeparator = " " };

    /// <summary>Whole numbers with a thin space between the thousands ("1 230"); empty for a missing value.</summary>
    public static string Fmt(int? value) => value.HasValue ? value.Value.ToString("N0", ThinSpace) : string.Empty;
}
