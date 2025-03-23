using Microsoft.AspNetCore.Components.Authorization;

using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace NED.WoT.BattleResults.Client.Services;

public class CustomAuthenticationStateProvider() : AuthenticationStateProvider
{
    private readonly HttpClient? _httpClient = new();
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static AuthenticationState? _authenticationState;

    public static void ResetAuthenticationState()
    {
        _authenticationState = null;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        DateTime lastAuthCheck = Preferences.Default.Get("LastAuthCheck", DateTime.MinValue);
        if (lastAuthCheck.Date < DateTime.Now.Date)
        {
            Preferences.Default.Set("LastAuthCheck", DateTime.Now);
            _authenticationState = null;
        }

        if (_authenticationState is not null)
        {
            return _authenticationState;
        }

        AuthResult? authResult = await CheckAuthenticationAsync();
        ClaimsIdentity identity = authResult is null || !authResult.CanLogin
            ? new ClaimsIdentity()
            : new ClaimsIdentity([
                new Claim(ClaimTypes.Name, Environment.UserName),
            ], "custom");

        if (authResult?.CanSeeBattleStats == true)
        {
            identity.AddClaim(new Claim(Claims.CanSeeBattleStats, bool.TrueString));
        }

        _authenticationState = new AuthenticationState(new ClaimsPrincipal(identity));
        return _authenticationState;
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

        string? apiUrl = "https://script.google.com/macros/s/AKfycbwi2lOBlb6G6X313FOKSirjJu5TCUXeNPsMwp7cKPHOOqLDmIpD3LTGfOlyEJHLofR1sQ/exec";
        string apiKey = "efb10f4a-04a0-450e-ad64-a46d326bac15";

        string url = $"{apiUrl}?apiKey={apiKey}&user={user}&machine={machine}&machineVersion={machineVersion}&machineKey={machineKey}";
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
