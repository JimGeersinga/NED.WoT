namespace NED.WoT.BattleResults.Client.Models;

public static class AppConfig
{
    /// <summary>The app's own Wargaming application (Mobile/Standalone "WoT Gevechtslog"). Not a secret.</summary>
    public const string WargamingApplicationId = "953ad65cc3872f5a909dc1921ba5acf7";

    /// <summary>EU only (docs/PLAN.md section 3). <c>auth/login</c> is on the same host.</summary>
    public const string WargamingApiBaseUrl = "https://api.worldoftanks.eu/wot/";

    /// <summary>WoT HQ production, HTTPS only (docs/PLAN.md §3.3).</summary>
    public const string HqBaseUrl = "https://wothq.code-breaker.nl/";
}
