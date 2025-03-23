using Microsoft.Extensions.Configuration;

using System.Security.Principal;
using System.Text.Json;

namespace NED.WoT.BattleResults.Client.Services;

public class AuthenticationService
{
    private readonly HttpClient? _httpClient;

    public AuthenticationService(IConfiguration configuration)
    {
        string? apiUrl = configuration.GetValue<string>("ApiUrl");
        if (!string.IsNullOrWhiteSpace(apiUrl))
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(apiUrl)
            };
        }
    }

    public async Task<AuthResult?> CheckAuthenticationAsync()
    {
        if (_httpClient is null)
        {
            return null;
        }

        string machine = Environment.MachineName;
        string? machineKey = WindowsIdentity.GetCurrent().User?.Value;
        string user = Environment.UserName;
        var info = DeviceInfo.Current;

        string response = await _httpClient.GetStringAsync($"?user={user}&machine={machine}");
        AuthResult? result = JsonSerializer.Deserialize<AuthResult>(response);
        return result;
    }

    public record AuthResult(string User, string Machine, DateTime RegisterDate, bool CanLogin, bool CanSeeBattleStats);
}
