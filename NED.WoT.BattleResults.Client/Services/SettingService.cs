using NED.WoT.BattleResults.Client.Models;

namespace NED.WoT.BattleResults.Client.Services
{
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
                ShowCopyNamesOnlyWhenClanMatches = Preferences.Default.Get(nameof(Settings.ShowCopyNamesOnlyWhenClanMatches), true),
                SingleBattleResultOpenedOnly = Preferences.Default.Get(nameof(Settings.SingleBattleResultOpenedOnly), false),
            };
        }
        public void Save(Settings settings)
        {
            Settings = settings;

            Preferences.Default.Set(nameof(Settings.WotReplayDirectory), Settings.WotReplayDirectory);
            Preferences.Default.Set(nameof(Settings.LoadBattlesSince), Settings.LoadBattlesSince.GetValueOrDefault().Ticks);
            Preferences.Default.Set(nameof(Settings.ClanAbbreviation), Settings.ClanAbbreviation);
            Preferences.Default.Set(nameof(Settings.PlayerName), Settings.PlayerName);
            Preferences.Default.Set(nameof(Settings.ShowCopyNamesOnlyWhenClanMatches), Settings.ShowCopyNamesOnlyWhenClanMatches);
            Preferences.Default.Set(nameof(Settings.SingleBattleResultOpenedOnly), Settings.SingleBattleResultOpenedOnly);
        }
    }
}
