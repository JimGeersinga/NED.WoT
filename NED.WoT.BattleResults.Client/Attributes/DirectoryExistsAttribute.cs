using System.ComponentModel.DataAnnotations;

namespace NED.WoT.BattleResults.Client.Attributes;

public class DirectoryExistsAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        return !string.IsNullOrWhiteSpace(value?.ToString()) && Directory.Exists(value.ToString());
    }
}
