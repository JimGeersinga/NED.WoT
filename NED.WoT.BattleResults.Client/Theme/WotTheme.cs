using System.Globalization;
using MudBlazor;

namespace NED.WoT.BattleResults.Client.Theme;

/// <summary>The WoT HQ design system as one MudTheme (docs/DESIGN.md). Dark only; the CSS-only tokens live in wwwroot/css/tokens.css.</summary>
public static class WotTheme
{
    private static readonly string[] Display = ["Barlow Condensed", "Rajdhani", "Arial Narrow", "sans-serif"];

    private static readonly string[] Text = ["Inter", "system-ui", "sans-serif"];

    public static MudTheme Theme { get; } = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#ff8800",
            PrimaryContrastText = "#121417",
            Secondary = "#6796ea",
            SecondaryContrastText = "#0b1222",
            Tertiary = "#b0916a",
            TertiaryContrastText = "#121417",
            Info = "#3fbfe6",
            InfoContrastText = "#121417",
            Success = "#4cc77a",
            SuccessContrastText = "#121417",
            Warning = "#f5c84c",
            WarningContrastText = "#121417",
            Error = "#ff6470",
            ErrorContrastText = "#121417",
            Dark = "#262c33",
            DarkContrastText = "#e6e9ec",
            Background = "#0e1114",
            BackgroundGray = "#0a0c0f",
            Surface = "#161a1f",
            AppbarBackground = "#14181d",
            AppbarText = "#e6e9ec",
            TextPrimary = "#e6e9ec",
            TextSecondary = "#a2abb5",
            TextDisabled = "#5d6670",
            ActionDefault = "#a2abb5",
            ActionDisabled = "#4f5761",
            ActionDisabledBackground = "#262c33",
            Divider = "#2a3139",
            DividerLight = "#20262c",
            LinesDefault = "#343c45",
            LinesInputs = "#6b7682",
            TableLines = "#232930",
            TableStriped = "rgba(255,255,255,.025)",
            TableHover = "rgba(255,255,255,.05)",
            OverlayDark = "rgba(6,8,10,.75)",
            Skeleton = "rgba(255,255,255,.07)",
        },
        Typography = new Typography
        {
            Default = Body(new DefaultTypography(), "0.9375rem", 400, "1.55", "0"),
            Body1 = Body(new Body1Typography(), "0.9375rem", 400, "1.55", "0"),
            Body2 = Body(new Body2Typography(), "0.8125rem", 400, "1.5", ".005em"),
            Subtitle1 = Body(new Subtitle1Typography(), "1rem", 500, "1.5", "0"),
            Subtitle2 = Body(new Subtitle2Typography(), "0.875rem", 600, "1.45", ".005em"),
            Caption = Body(new CaptionTypography(), "0.75rem", 400, "1.4", ".01em"),
            H1 = Head(new H1Typography(), "3rem", 700, "1.05", "0"),
            H2 = Head(new H2Typography(), "2.375rem", 700, "1.1", "0"),
            H3 = Head(new H3Typography(), "1.875rem", 600, "1.15", "0"),
            H4 = Head(new H4Typography(), "1.5rem", 600, "1.2", ".005em"),
            H5 = Head(new H5Typography(), "1.25rem", 600, "1.25", ".01em"),
            H6 = Head(new H6Typography(), "1.0625rem", 600, "1.3", ".04em", "uppercase"),
            Button = Head(new ButtonTypography(), "0.9375rem", 600, "1.2", ".05em", "uppercase"),
            Overline = Head(new OverlineTypography(), "0.75rem", 600, "1.6", ".12em", "uppercase"),
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "4px",
            AppbarHeight = "56px",
        },
        Shadows = new MudBlazor.Shadow
        {
            // MudBlazor indexes 0..25: none, then a 1px hairline for every elevation 1-25; the real shadow is on dialogs and popovers only (wwwroot/css/mud.css).
            Elevation = ["none", .. Enumerable.Repeat("0 0 0 1px rgba(255,255,255,.06)", 25)],
        },
    };

    private static T Body<T>(T style, string size, int weight, string lineHeight, string spacing)
        where T : BaseTypography
    {
        return Style(style, Text, size, weight, lineHeight, spacing, "none");
    }

    private static T Head<T>(T style, string size, int weight, string lineHeight, string spacing, string transform = "none")
        where T : BaseTypography
    {
        return Style(style, Display, size, weight, lineHeight, spacing, transform);
    }

    private static T Style<T>(T style, string[] family, string size, int weight, string lineHeight, string spacing, string transform)
        where T : BaseTypography
    {
        style.FontFamily = family;
        style.FontSize = size;
        style.FontWeight = weight.ToString(CultureInfo.InvariantCulture);
        style.LineHeight = lineHeight;
        style.LetterSpacing = spacing;
        style.TextTransform = transform;
        return style;
    }
}
