using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using MudBlazor.Services;

using NED.WoT.BattleResults.Client.Services;

namespace NED.WoT.BattleResults.Client;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // Snackbars (docs/DESIGN.md, "snackbar"): bottom left, outlined, filled state icons, no close button, opaque (the look is in mud.css).
        builder.Services.AddMudServices(config =>
        {
            config.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomLeft;
            config.SnackbarConfiguration.SnackbarVariant = MudBlazor.Variant.Outlined;
            config.SnackbarConfiguration.MaximumOpacity = 100;
            config.SnackbarConfiguration.ShowCloseIcon = false;
            config.SnackbarConfiguration.InfoIcon = MudBlazor.Icons.Material.Filled.Info;
            config.SnackbarConfiguration.SuccessIcon = MudBlazor.Icons.Material.Filled.CheckCircle;
            config.SnackbarConfiguration.WarningIcon = MudBlazor.Icons.Material.Filled.Warning;
            config.SnackbarConfiguration.ErrorIcon = MudBlazor.Icons.Material.Filled.Error;
        });
        builder.Services.AddAuthorizationCore();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddSingleton<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
        builder.Services.AddScoped<BattleReportService>();
        builder.Services.AddScoped<UpdateCheckService>();
        builder.Services.AddSingleton<SettingService>();
        builder.Services.AddSingleton<HttpClient>();
        builder.Services.AddSingleton<WargamingAuthService>();

        return builder.Build();
    }
}
