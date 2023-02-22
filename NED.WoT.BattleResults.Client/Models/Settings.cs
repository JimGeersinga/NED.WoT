namespace NED.WoT.BattleResults.Client.Models
{
    public class Settings
    {
        public string WotReplayDirectory { get; set; }
        public DateTime? LoadBattlesSince { get; set; }
        public string ClanAbbreviation { get; set; }
        public string PlayerName { get; set; }
        public bool ShowCopyNamesOnlyWhenClanMatches { get; set; }
    }
}
