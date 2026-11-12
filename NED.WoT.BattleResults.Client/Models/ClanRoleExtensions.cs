namespace NED.WoT.BattleResults.Client.Models;

public static class ClanRoleExtensions
{
    extension(ClanRole role)
    {
        /// <summary>Maps a Wargaming snake_case role (<c>executive_officer</c>); null or anything unrecognised is <see cref="ClanRole.Unknown"/>.</summary>
        public static ClanRole Parse(string? wgRole)
        {
            return wgRole switch
            {
                "commander" => ClanRole.Commander,
                "executive_officer" => ClanRole.ExecutiveOfficer,
                "personnel_officer" => ClanRole.PersonnelOfficer,
                "combat_officer" => ClanRole.CombatOfficer,
                "intelligence_officer" => ClanRole.IntelligenceOfficer,
                "quartermaster" => ClanRole.Quartermaster,
                "recruitment_officer" => ClanRole.RecruitmentOfficer,
                "junior_officer" => ClanRole.JuniorOfficer,
                "private" => ClanRole.Private,
                "recruit" => ClanRole.Recruit,
                "reservist" => ClanRole.Reservist,
                _ => ClanRole.Unknown,
            };
        }

        /// <summary>WoT's English name: <c>CombatOfficer</c> becomes <c>Combat Officer</c>.</summary>
        public string DisplayName() =>
            string.Concat(role.ToString().Select((c, i) => i > 0 && char.IsUpper(c) ? $" {c}" : c.ToString()));
    }
}
