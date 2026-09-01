using BlazorBase.User.Models;

namespace BlazorBase.User.Services;

public interface IAuthService
{
    Task<AuthStatusResponse> GetStatusAsync();
    Task<LoginResponse> SetupAsync(SetupRequest request);
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<LoginResponse> RefreshAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);

    /// <summary>
    /// Signs in as the server's configured development account. Returns null when the server
    /// does not offer that path, which is the case in every environment but Development.
    /// </summary>
    Task<LoginResponse?> DevelopmentLoginAsync();

    /// <summary>
    /// Asks the server who the caller currently is. The only way to obtain the claims under the
    /// cookie transport, where the access token is deliberately unreadable by script.
    /// </summary>
    Task<AuthSessionResponse> GetSessionAsync();
}
