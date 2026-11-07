using NED.WoT.BattleResults.Client.Models;
using System.Diagnostics;
using System.Text.Json;

namespace NED.WoT.BattleResults.Client.Services;

public class SettingService
{
    // The single replay path of before #15; it is read once to make the first folder and never written again.
    private const string LegacyReplayDirectoryKey = "WotReplayDirectory";

    public Settings Settings { get; set; }

    public SettingService()
    {
        SelfCheck();
        Settings = new Settings
        {
            ReplayFolders = ParseFolders(Preferences.Default.Get(nameof(Settings.ReplayFolders), string.Empty), Preferences.Default.Get(LegacyReplayDirectoryKey, string.Empty)),
            LoadBattlesSince = new DateTime(Preferences.Default.Get(nameof(Settings.LoadBattlesSince), DateTime.Now.AddDays(-10).Date.Ticks)),
            ClanAbbreviation = Preferences.Default.Get(nameof(Settings.ClanAbbreviation), "NED-1"),
            PlayerName = Preferences.Default.Get(nameof(Settings.PlayerName), string.Empty),
            SingleBattleResultOpenedOnly = Preferences.Default.Get(nameof(Settings.SingleBattleResultOpenedOnly), false),
            OnlyHighlistOwnMatches = Preferences.Default.Get(nameof(Settings.OnlyHighlistOwnMatches), false),
            UpdateScreeenWhileLoading = Preferences.Default.Get(nameof(Settings.UpdateScreeenWhileLoading), true),
            AutoClanLookup = Preferences.Default.Get(nameof(Settings.AutoClanLookup), false)
        };
    }

    public void Save()
    {
        Preferences.Default.Set(nameof(Settings.ReplayFolders), JsonSerializer.Serialize(Settings.ReplayFolders));
        Preferences.Default.Set(nameof(Settings.LoadBattlesSince), Settings.LoadBattlesSince.GetValueOrDefault().Ticks);
        Preferences.Default.Set(nameof(Settings.ClanAbbreviation), Settings.ClanAbbreviation);
        Preferences.Default.Set(nameof(Settings.PlayerName), Settings.PlayerName);
        Preferences.Default.Set(nameof(Settings.SingleBattleResultOpenedOnly), Settings.SingleBattleResultOpenedOnly);
        Preferences.Default.Set(nameof(Settings.OnlyHighlistOwnMatches), Settings.OnlyHighlistOwnMatches);
        Preferences.Default.Set(nameof(Settings.UpdateScreeenWhileLoading), Settings.UpdateScreeenWhileLoading);
        Preferences.Default.Set(nameof(Settings.AutoClanLookup), Settings.AutoClanLookup);
    }

    /// <summary>The saved folders; without them (nothing saved yet, or unreadable) the old single path becomes one folder "Replays".</summary>
    private static List<ReplayFolder> ParseFolders(string? json, string? legacyPath)
    {
        List<ReplayFolder>? folders = null;
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                folders = JsonSerializer.Deserialize<List<ReplayFolder>>(json);
            }
            catch (JsonException)
            {
                // Treated as absent: the old path or the first-run dialog takes over.
            }
        }

        if (folders is { Count: > 0 })
        {
            return folders;
        }

        return string.IsNullOrWhiteSpace(legacyPath) ? [] : [new ReplayFolder { Title = "Replays", Path = legacyPath }];
    }

    [Conditional("DEBUG")]
    private static void SelfCheck()
    {
        var migrated = ParseFolders(null, @"C:\Games\World_of_Tanks_EU\replays");
        Debug.Assert(migrated is [{ Title: "Replays", Path: @"C:\Games\World_of_Tanks_EU\replays" }], "old path becomes one folder");
        Debug.Assert(ParseFolders(string.Empty, "  ").Count == 0, "no folders and no old path: first run");
        Debug.Assert(ParseFolders("not json", null).Count == 0, "unreadable JSON counts as absent");

        var saved = JsonSerializer.Serialize(new List<ReplayFolder> { new() { Title = "A", Path = "B" }, new() { Title = "C", Path = "D" } });
        var loaded = ParseFolders(saved, @"C:\old");
        Debug.Assert(loaded is [{ Title: "A", Path: "B" }, { Title: "C", Path: "D" }], "saved folders win over the old path, in order");
    }
}
