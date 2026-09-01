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

    /// <summary>
    /// A failure here means "not signed in", never an exception: this is called on every render of a
    /// guarded page, and throwing would turn a transient network blip into a broken application
    /// rather than a login prompt.
    /// </summary>
    public async Task<AuthSessionResponse> GetSessionAsync()
    {
        try
        {
            var response = await HttpClient.GetAsync("api/auth/session");

            if (!response.IsSuccessStatusCode)
                return new AuthSessionResponse();

            return await response.Content.ReadFromJsonAsync<AuthSessionResponse>()
                ?? new AuthSessionResponse();
        }
        catch (HttpRequestException)
        {
            return new AuthSessionResponse();
        }
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

    public async Task<AuthServiceLoginOutcome> LoginAsync(LoginRequest request)
    {
        var response = await HttpClient.PostAsJsonAsync("api/auth/login", request);

        if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
        {
            var pending = await response.Content.ReadFromJsonAsync<TwoFactorRequiredResponse>()
                ?? throw new InvalidOperationException("Invalid two-factor response.");
            return AuthServiceLoginOutcome.TwoFactorRequired(pending.PendingToken);
        }

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

        var tokens = await response.Content.ReadFromJsonAsync<LoginResponse>()
            ?? throw new InvalidOperationException("Invalid login response.");

        return AuthServiceLoginOutcome.Completed(tokens);
    }

    public async Task<LoginResponse> LoginTwoFactorAsync(TwoFactorLoginRequest request)
    {
        var response = await HttpClient.PostAsJsonAsync("api/auth/login/2fa", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoginResponse>()
            ?? throw new InvalidOperationException("Invalid two-factor login response.");
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

    public async Task ConfirmEmailAsync(ConfirmEmailRequest request)
    {
        var response = await HttpClient.PostAsJsonAsync("api/auth/manage/confirm-email", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResendConfirmationAsync(ResendConfirmationRequest request)
    {
        var response = await HttpClient.PostAsJsonAsync("api/auth/manage/resend-confirmation", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var response = await HttpClient.PostAsJsonAsync("api/auth/manage/forgot-password", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        var response = await HttpClient.PostAsJsonAsync("api/auth/manage/reset-password", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task<TwoFactorStatusResponse> GetTwoFactorStatusAsync()
    {
        var response = await HttpClient.GetAsync("api/auth/manage/2fa");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TwoFactorStatusResponse>()
            ?? throw new InvalidOperationException("Invalid two-factor status response.");
    }

    public async Task<TwoFactorRecoveryCodesResponse> EnableTwoFactorAsync(TwoFactorEnableRequest request)
    {
        var response = await HttpClient.PostAsJsonAsync("api/auth/manage/2fa/enable", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TwoFactorRecoveryCodesResponse>()
            ?? throw new InvalidOperationException("Invalid two-factor enable response.");
    }

    public async Task DisableTwoFactorAsync()
    {
        var response = await HttpClient.PostAsync("api/auth/manage/2fa/disable", content: null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<TwoFactorRecoveryCodesResponse> RegenerateRecoveryCodesAsync()
    {
        var response = await HttpClient.PostAsync("api/auth/manage/2fa/recovery-codes", content: null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TwoFactorRecoveryCodesResponse>()
            ?? throw new InvalidOperationException("Invalid recovery-codes response.");
    }
}
