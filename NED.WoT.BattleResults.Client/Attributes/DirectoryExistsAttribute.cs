using System.ComponentModel.DataAnnotations;

namespace NED.WoT.BattleResults.Client.Attributes;

/// <summary>The value is a folder that exists. An empty value passes: [Required] reports that one.</summary>
public class DirectoryExistsAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        string? path = value?.ToString();
        return string.IsNullOrWhiteSpace(path) || Directory.Exists(path);
    }
}
