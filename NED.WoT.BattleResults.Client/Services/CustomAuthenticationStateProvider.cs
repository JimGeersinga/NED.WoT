using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;

using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace NED.WoT.BattleResults.Client.Services;

public class CustomAuthenticationStateProvider(IConfiguration Configuration) : AuthenticationStateProvider
{
    private readonly HttpClient? _httpClient = new();
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private AuthenticationState? _authenticationState;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if(_authenticationState?.User?.Identity is not null && _authenticationState.User.Identity.IsAuthenticated)
        {
            return _authenticationState;
        }

        AuthResult? authResult = await CheckAuthenticationAsync();
        ClaimsIdentity identity = authResult is null || !authResult.CanLogin
            ? new ClaimsIdentity()
            : new ClaimsIdentity([new Claim(ClaimTypes.Name, "user")], "custom");

        _authenticationState = new AuthenticationState(new ClaimsPrincipal(identity));
        return _authenticationState;
    }

    public void RetryAuthentication()
    {
        _authenticationState = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task<AuthResult?> CheckAuthenticationAsync()
    {
        if (_httpClient is null)
        {
            return null;
        }

        string user = Environment.UserName;
        string machine = Environment.MachineName;
        string machineVersion = DeviceInfo.Current.VersionString;
        string machineKey = Hash(WindowsIdentity.GetCurrent().User?.Value);
        if (string.IsNullOrWhiteSpace(machineKey))
        {
            return null;
        }

        string? apiUrl = Configuration.GetValue<string>("ApiUrl");
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            return null;
        }

        string url = $"{apiUrl}?user={user}&machine={machine}&machineVersion={machineVersion}&machineKey={machineKey}";
        string response = await _httpClient.GetStringAsync(url);
        try
        {
            return JsonSerializer.Deserialize<AuthResult>(response, _jsonSerializerOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Hash(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        byte[] valueBytes = Encoding.UTF8.GetBytes(value);
        byte[] hashBytes = SHA256.HashData(valueBytes);
        return Convert.ToBase64String(hashBytes);
    }

    public record AuthResult(bool CanLogin, bool CanSeeBattleStats);
}
