using Microsoft.Extensions.Logging;

using MudBlazor.Services;

using NED.WoT.BattleResults.Client.Services;

namespace NED.WoT.BattleResults.Client;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        builder.Services.AddMudServices();
        builder.Services.AddScoped<BattleReportService>();
        builder.Services.AddSingleton<SettingService>();

        return builder.Build();
    }
}
