namespace NED.WoT.BattleResults.Client.Models
{
    public class BattleReportRemovedEventArgs : EventArgs
    {
        public BattleReport Report { get; }

        public BattleReportRemovedEventArgs(BattleReport report)
        {
            Report = report;
        }
    }
}
