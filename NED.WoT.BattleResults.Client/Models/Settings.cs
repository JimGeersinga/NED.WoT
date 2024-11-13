namespace NED.WoT.BattleResults.Client.Models
{
    public class Settings
    {
        public string WotReplayDirectory { get; set; }
        public DateTime? LoadBattlesSince { get; set; }
        public string ClanAbbreviation { get; set; }
        public string PlayerName { get; set; }
        public bool ShowCopyNamesOnlyWhenClanMatches { get; set; }
        public bool SingleBattleResultOpenedOnly { get; set; }
        public bool OnlyHighlistOwnMatches { get; set; }
        public bool UpdateScreeenWhileLoading { get; set; }
    }
}
