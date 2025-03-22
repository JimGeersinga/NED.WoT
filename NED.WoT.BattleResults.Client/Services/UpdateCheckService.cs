using Windows.ApplicationModel;

namespace NED.WoT.BattleResults.Client.Services;

public class UpdateCheckService
{
    private readonly CancellationTokenSource _cts = new();
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15);

    public bool UpdateAvailable { get; set; }

    public void Start()
    {
        Task.Run(async () =>
        {
            await Task.Delay(5000);
            while (!_cts.Token.IsCancellationRequested && !UpdateAvailable)
            {
                await CheckForUpdateAsync();
                await Task.Delay(_checkInterval, _cts.Token);
            }
        }, _cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    private async Task CheckForUpdateAsync()
    {
        PackageUpdateAvailabilityResult result = await Package.Current.CheckUpdateAvailabilityAsync();
        UpdateAvailable = result.Availability == PackageUpdateAvailability.Available;
    }
}
