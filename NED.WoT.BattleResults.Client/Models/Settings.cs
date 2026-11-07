using System.ComponentModel.DataAnnotations;

namespace NED.WoT.BattleResults.Client.Models;

public class Settings
{
    /// <summary>The replay folders in the order of the carousel in the app bar; the app starts on the first one.</summary>
    public List<ReplayFolder> ReplayFolders { get; set; } = [];

    [Required(ErrorMessage = "Datum is verplicht")]
    public DateTime? LoadBattlesSince { get; set; }

    public string? ClanAbbreviation { get; set; }

    public string? PlayerName { get; set; }

    public bool SingleBattleResultOpenedOnly { get; set; }

    public bool OnlyHighlistOwnMatches { get; set; }

    public bool UpdateScreeenWhileLoading { get; set; }

    public bool AutoClanLookup { get; set; }
}
