using Windows.ApplicationModel;

namespace NED.WoT.BattleResults.Client.Services;

public class UpdateCheckService : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15);

    public bool UpdateAvailable { get; set; }

    public void Start()
    {
        // The token is a copy: the loop keeps working on it after Dispose has disposed the source.
        CancellationToken token = _cts.Token;
        Task.Run(async () =>
        {
            await Task.Delay(5000, token);
            while (!token.IsCancellationRequested && !UpdateAvailable)
            {
                await CheckForUpdateAsync();
                await Task.Delay(_checkInterval, token);
            }
        }, token);
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    /// <summary>A reload makes a new DI scope; the scope disposes this one, which ends the check loop.</summary>
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task CheckForUpdateAsync()
    {
        PackageUpdateAvailabilityResult result = await Package.Current.CheckUpdateAvailabilityAsync();
        UpdateAvailable = result.Availability == PackageUpdateAvailability.Available;
    }
}
