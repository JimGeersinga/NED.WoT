using NED.WoT.BattleResults.Client.Models;

namespace NED.WoT.BattleResults.Client.Services;

public enum IdentityStatus { SignedOut, Allowed, Refused, Unavailable }

/// <summary>
/// Who the signed-in Wargaming player is, and whether WoT HQ lets them in (docs/PLAN.md §3.3). HQ is the only authority:
/// no answer means no access, and nothing of the last answer is kept. Asked at every start and once a day while running.
/// </summary>
public sealed class IdentityService(WargamingAuthService auth, GevechtslogSessionClient hq) : IDisposable
{
    private static readonly TimeSpan DailyCheck = TimeSpan.FromDays(1);
    private Timer? _daily;

    /// <summary>HQ's identity; set for <see cref="IdentityStatus.Allowed"/> and <see cref="IdentityStatus.Refused"/>.</summary>
    public HqIdentity? Current { get; private set; }

    public IdentityStatus Status { get; private set; }

    public event Action? Changed;

    /// <summary>Loads the stored session and asks HQ. False when there is no session.</summary>
    public async Task<bool> InitializeAsync()
    {
        _daily ??= new Timer(_ => _ = RefreshAsync(), null, DailyCheck, DailyCheck);
        return await ApplyAsync(await auth.GetSessionAsync());
    }

    /// <summary>Browser sign-in, then asks HQ. False when refused, timed out or cancelled, or when HQ calls the token invalid.</summary>
    public async Task<bool> SignInAsync(CancellationToken cancellationToken) => await ApplyAsync(await auth.SignInAsync(cancellationToken));

    /// <summary>Asks HQ again (the daily check, "Opnieuw proberen").</summary>
    public async Task RefreshAsync() => await ApplyAsync(await auth.GetSessionAsync());

    public void SignOut()
    {
        WargamingAuthService.SignOut();
        Set(IdentityStatus.SignedOut, null);
    }

    public void Dispose() => _daily?.Dispose();

    private async Task<bool> ApplyAsync(WargamingSession? session)
    {
        if (session is null)
        {
            Set(IdentityStatus.SignedOut, null);
            return false;
        }

#if DEBUG
        // Development without HQ: Allowed with advanced stats and no clan.
        if (Environment.GetEnvironmentVariable("GEVECHTSLOG_SKIP_HQ") == "1")
        {
            Set(IdentityStatus.Allowed, new HqIdentity(session.AccountId, session.Nickname, null, null, null, null, ClanRole.Unknown, true));
            return true;
        }
#endif

        switch (await hq.StartAsync(session, CancellationToken.None))
        {
            case SessionResult.Allowed a:
                Set(IdentityStatus.Allowed, a.Identity);
                return true;
            case SessionResult.Refused r:
                Set(IdentityStatus.Refused, r.Identity);
                return true;
            case SessionResult.InvalidToken:
                SignOut();
                return false;
            default:
                Set(IdentityStatus.Unavailable, null);
                return true;
        }
    }

    private void Set(IdentityStatus status, HqIdentity? identity)
    {
        Status = status;
        Current = identity;
        Changed?.Invoke();
    }
}
