using NED.WoT.BattleResults.Client.Models;

namespace NED.WoT.BattleResults.Client.Services;

public class SettingService
{
    public Settings Settings { get; set; }

    public SettingService()
    {
        Settings = new Settings
        {
            WotReplayDirectory = Preferences.Default.Get(nameof(Settings.WotReplayDirectory), string.Empty),
            LoadBattlesSince = new DateTime(Preferences.Default.Get(nameof(Settings.LoadBattlesSince), DateTime.Now.AddDays(-10).Date.Ticks)),
            ClanAbbreviation = Preferences.Default.Get(nameof(Settings.ClanAbbreviation), "NED-1"),
            PlayerName = Preferences.Default.Get(nameof(Settings.PlayerName), string.Empty),
            SingleBattleResultOpenedOnly = Preferences.Default.Get(nameof(Settings.SingleBattleResultOpenedOnly), false),
            OnlyHighlistOwnMatches = Preferences.Default.Get(nameof(Settings.OnlyHighlistOwnMatches), false),
            UpdateScreeenWhileLoading = Preferences.Default.Get(nameof(Settings.UpdateScreeenWhileLoading), true),
            AutoClanLookup = Preferences.Default.Get(nameof(Settings.AutoClanLookup), false),
            IsDarkMode = Preferences.Default.Get(nameof(Settings.IsDarkMode), true)
        };
    }

    public void Save()
    {
        Preferences.Default.Set(nameof(Settings.WotReplayDirectory), Settings.WotReplayDirectory);
        Preferences.Default.Set(nameof(Settings.LoadBattlesSince), Settings.LoadBattlesSince.GetValueOrDefault().Ticks);
        Preferences.Default.Set(nameof(Settings.ClanAbbreviation), Settings.ClanAbbreviation);
        Preferences.Default.Set(nameof(Settings.PlayerName), Settings.PlayerName);
        Preferences.Default.Set(nameof(Settings.SingleBattleResultOpenedOnly), Settings.SingleBattleResultOpenedOnly);
        Preferences.Default.Set(nameof(Settings.OnlyHighlistOwnMatches), Settings.OnlyHighlistOwnMatches);
        Preferences.Default.Set(nameof(Settings.UpdateScreeenWhileLoading), Settings.UpdateScreeenWhileLoading);
        Preferences.Default.Set(nameof(Settings.AutoClanLookup), Settings.AutoClanLookup);
        Preferences.Default.Set(nameof(Settings.IsDarkMode), Settings.IsDarkMode);
    }
}
