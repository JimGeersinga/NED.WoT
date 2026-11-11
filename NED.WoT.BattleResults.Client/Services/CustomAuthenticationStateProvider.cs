using Microsoft.AspNetCore.Components.Authorization;

using System.Security.Claims;

namespace NED.WoT.BattleResults.Client.Services;

/// <summary>The principal follows <see cref="IdentityService"/>, i.e. what WoT HQ answered. Not signed in or HQ unreachable: anonymous.</summary>
public sealed class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IdentityService _identity;

    public CustomAuthenticationStateProvider(IdentityService identity)
    {
        _identity = identity;
        identity.Changed += () => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        if (_identity.Status is IdentityStatus.Allowed or IdentityStatus.Refused && _identity.Current is { } who)
        {
            var id = new ClaimsIdentity([new Claim(ClaimTypes.Name, who.Nickname)], "hq");
            if (who.CanUse)
            {
                id.AddClaim(new Claim(Claims.CanUse, bool.TrueString));
                if (who.CanSeeAdvancedStats)
                {
                    id.AddClaim(new Claim(Claims.CanSeeBattleStats, bool.TrueString));
                }
            }

            principal = new ClaimsPrincipal(id);
        }

        return Task.FromResult(new AuthenticationState(principal));
    }
}
