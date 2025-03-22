namespace NED.WoT.BattleResults.Client;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage()) { Title = $"V{AppInfo.VersionString}" };
    }
}
