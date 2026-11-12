namespace NED.WoT.BattleResults.Client.Models;

/// <summary>A clan role. The name is also the file name of its icon in <c>wwwroot/img/ranks</c>.</summary>
public enum ClanRole
{
    Unknown,
    Commander,
    ExecutiveOfficer,
    PersonnelOfficer,
    CombatOfficer,
    IntelligenceOfficer,
    Quartermaster,
    RecruitmentOfficer,
    JuniorOfficer,
    Private,
    Recruit,
    Reservist,
}

public static class ClanRoles
{
    /// <summary>Wargaming's spelling ("combat_officer") to the role; <see cref="ClanRole.Unknown"/> for null or a role we don't know.</summary>
    public static ClanRole Parse(string? wargamingRole) =>
        wargamingRole is not null && Enum.TryParse(wargamingRole.Replace("_", ""), ignoreCase: true, out ClanRole role) && Enum.IsDefined(role)
            ? role
            : ClanRole.Unknown;
}
