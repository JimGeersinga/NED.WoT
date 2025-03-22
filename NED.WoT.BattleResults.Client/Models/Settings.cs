using NED.WoT.BattleResults.Client.Attributes;
using System.ComponentModel.DataAnnotations;

namespace NED.WoT.BattleResults.Client.Models;

public class Settings
{
    [Required(ErrorMessage = "WoT replay path is verplicht!")]
    [DirectoryExists(ErrorMessage = "WoT replay path is niet geldig!")]
    public string? WotReplayDirectory { get; set; }

    [Required(ErrorMessage = "Datum is verplicht!")]
    public DateTime? LoadBattlesSince { get; set; }

    public string? ClanAbbreviation { get; set; }

    public string? PlayerName { get; set; }

    public bool SingleBattleResultOpenedOnly { get; set; }

    public bool OnlyHighlistOwnMatches { get; set; }

    public bool UpdateScreeenWhileLoading { get; set; }

    public bool AutoClanLookup { get; set; }

    public bool IsDarkMode { get; set; }

    public bool ShowBattleStats { get; set; }
}
