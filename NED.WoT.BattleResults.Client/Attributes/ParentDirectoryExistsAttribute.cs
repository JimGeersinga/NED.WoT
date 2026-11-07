using System.ComponentModel.DataAnnotations;

namespace NED.WoT.BattleResults.Client.Attributes;

/// <summary>The value is a file path whose folder exists; the file itself need not. An empty value passes.</summary>
public class ParentDirectoryExistsAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        string? path = value?.ToString();
        return string.IsNullOrWhiteSpace(path) || Directory.Exists(Path.GetDirectoryName(path));
    }
}
