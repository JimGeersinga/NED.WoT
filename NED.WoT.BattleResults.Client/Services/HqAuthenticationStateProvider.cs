using Microsoft.AspNetCore.Components.Authorization;

using System.Security.Claims;

namespace NED.WoT.BattleResults.Client.Services;

/// <summary>The principal follows <see cref="IdentityService"/>, i.e. what WoT HQ answered. Authenticated only when HQ allowed the account; otherwise anonymous.</summary>
public sealed class HqAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IdentityService _identity;

    public HqAuthenticationStateProvider(IdentityService identity)
    {
        _identity = identity;
        identity.Changed += () => NotifyAuthenticationStateChanged(Task.FromResult(Build()));
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(Build());

    private AuthenticationState Build()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        if (_identity is { Status: IdentityStatus.Allowed, Current: { } who })
        {
            var id = new ClaimsIdentity([new Claim(ClaimTypes.Name, who.Nickname)], "hq");
            if (who.CanSeeAdvancedStats)
            {
                id.AddClaim(new Claim(Claims.CanSeeBattleStats, bool.TrueString));
            }

            principal = new ClaimsPrincipal(id);
        }

        return new AuthenticationState(principal);
    }
}
