using NED.WoT.BattleResults.Client.Attributes;
using System.ComponentModel.DataAnnotations;

namespace NED.WoT.BattleResults.Client.Models;

public class Settings
{
    /// <summary>The replay folders in the order of the carousel in the app bar; the app starts on the first one.</summary>
    public List<ReplayFolder> ReplayFolders { get; set; } = [];

    public string? ClanAbbreviation { get; set; }

    public string? PlayerName { get; set; }

    public bool SingleBattleResultOpenedOnly { get; set; }

    public bool OnlyHighlistOwnMatches { get; set; }

    public bool UpdateScreeenWhileLoading { get; set; }

    public bool AutoClanLookup { get; set; }

    /// <summary>The temp.wotreplay of the running battle, wherever the game writes it; empty means temp.wotreplay in the first replay folder. The file exists only during a battle, so only its folder is checked.</summary>
    [ParentDirectoryExists(ErrorMessage = "Deze map bestaat niet.")]
    public string? LiveReplayPath { get; set; }
}
