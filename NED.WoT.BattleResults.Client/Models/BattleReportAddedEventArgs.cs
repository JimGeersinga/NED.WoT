namespace NED.WoT.BattleResults.Client.Models;

public class BattleReportAddedEventArgs : EventArgs
{
    public BattleReport Report { get; }

    public BattleReportAddedEventArgs(BattleReport report)
    {
        Report = report;
    }
}
