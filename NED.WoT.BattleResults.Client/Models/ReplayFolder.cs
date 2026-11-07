using NED.WoT.BattleResults.Client.Attributes;
using System.ComponentModel.DataAnnotations;

namespace NED.WoT.BattleResults.Client.Models;

/// <summary>One replay folder of the Settings: a title for the app bar and the path of the folder.</summary>
public class ReplayFolder
{
    [Required(ErrorMessage = "Titel is verplicht.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Pad is verplicht.")]
    [DirectoryExists(ErrorMessage = "Deze map bestaat niet.")]
    public string Path { get; set; } = string.Empty;
}
