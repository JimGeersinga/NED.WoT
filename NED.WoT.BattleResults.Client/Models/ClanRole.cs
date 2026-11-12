namespace NED.WoT.BattleResults.Client.Models;

/// <summary>A clan role. The name is also the file name of its icon in <c>wwwroot/img/ranks</c>. Wargaming parsing: <c>ClanRole.Parse</c> in <c>ClanRoleExtensions</c>.</summary>
public enum ClanRole
{
    Unknown = 0,
    Commander = 1,
    ExecutiveOfficer = 2,
    PersonnelOfficer = 3,
    CombatOfficer = 4,
    IntelligenceOfficer = 5,
    Quartermaster = 6,
    RecruitmentOfficer = 7,
    JuniorOfficer = 8,
    Private = 9,
    Recruit = 10,
    Reservist = 11,
}
