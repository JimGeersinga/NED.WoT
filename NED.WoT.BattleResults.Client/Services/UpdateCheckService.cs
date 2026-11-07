using Microsoft.Extensions.Logging;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace NED.WoT.BattleResults.Client.Services;

public partial class UpdateCheckService(ILogger<UpdateCheckService> logger) : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15);

    public bool UpdateAvailable { get; private set; }

    /// <summary>Raised (on a background thread) when <see cref="UpdateAvailable"/> turns true.</summary>
    public event Action? Changed;

    public void Start()
    {
        // The token is a copy: the loop keeps working on it after Dispose has disposed the source.
        CancellationToken token = _cts.Token;
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(5000, token);
                while (!token.IsCancellationRequested && !UpdateAvailable)
                {
                    await CheckOnceAsync();
                    await Task.Delay(_checkInterval, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Dispose ended the loop.
            }
        }, token);
    }

    /// <summary>A reload makes a new DI scope; the scope disposes this one, which ends the check loop.</summary>
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>A failed check is logged and the loop tries again on the next interval.</summary>
    private async Task CheckOnceAsync()
    {
        try
        {
            // Package.Current.CheckUpdateAvailabilityAsync fails with "Access denied"; ask the system for the same package instead.
            Package package = new PackageManager().FindPackageForUser(string.Empty, Package.Current.Id.FullName);
            PackageUpdateAvailabilityResult result = await package.CheckUpdateAvailabilityAsync();
            if (result.Availability == PackageUpdateAvailability.Available)
            {
                UpdateAvailable = true;
                Changed?.Invoke();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogCheckFailed(ex.GetType().Name, ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Update check failed: {Type}: {Message}")]
    private partial void LogCheckFailed(string type, string message);
}
