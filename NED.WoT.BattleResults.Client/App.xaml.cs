namespace NED.WoT.BattleResults.Client;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage()) { Title = Title() };
    }

    private static string Title(string? suffix = null) => $"WoT Gevechtslog · V{AppInfo.Current.Version.ToString(3)}{suffix}";

    /// <summary>The window title with something behind the version (the load time); null is the plain title.</summary>
    public static void SetTitle(string? suffix) => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (Current is { Windows: [var window, ..] })
        {
            window.Title = Title(suffix);
        }
    });
}
