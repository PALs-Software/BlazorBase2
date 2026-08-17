using System.Net.Http.Json;
using System.Text.Json;
using BlazorBase.User.Models;

namespace BlazorBase.User.Services;

public class AuthService(HttpClient httpClient) : IAuthService
{
    #region Injects
    private readonly HttpClient HttpClient = httpClient;
    #endregion

    public async Task<AuthStatusResponse> GetStatusAsync()
    {
        var response = await HttpClient.GetAsync("api/auth/status");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AuthStatusResponse>()
            ?? new AuthStatusResponse();
    }

    public async Task<LoginResponse> SetupAsync(SetupRequest request)
    {
        var response = await HttpClient.PostAsJsonAsync("api/auth/setup", request);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("errors", out var errors))
                    throw new HttpRequestException(string.Join(" ", errors.EnumerateArray().Select(e => e.GetString())));
                if (doc.RootElement.TryGetProperty("message", out var message))
                    throw new HttpRequestException(message.GetString());
            }
            catch (JsonException) { }

            response.EnsureSuccessStatusCode();
        }

        return await response.Content.ReadFromJsonAsync<LoginResponse>()
            ?? throw new InvalidOperationException("Invalid setup response.");
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var response = await HttpClient.PostAsJsonAsync("api/auth/login", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoginResponse>()
            ?? throw new InvalidOperationException("Invalid login response.");
    }

    public async Task<LoginResponse> RefreshAsync(string refreshToken)
    {
        var request = new RefreshTokenRequest { RefreshToken = refreshToken };
        var response = await HttpClient.PostAsJsonAsync("api/auth/refresh", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoginResponse>()
            ?? throw new InvalidOperationException("Invalid refresh response.");
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var request = new RefreshTokenRequest { RefreshToken = refreshToken };
        await HttpClient.PostAsJsonAsync("api/auth/logout", request);
    }

    public async Task<LoginResponse?> DevelopmentLoginAsync()
    {
        var response = await HttpClient.PostAsync("api/auth/development-login", content: null);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<LoginResponse>();
    }
}
