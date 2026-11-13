namespace BlazorBase.User.Services;

/// <summary>
/// The framework sign-out path: <c>POST api/auth/logout</c> first, then
/// <see cref="BlazorBaseUserAuthStateProvider.MarkUserAsLoggedOut"/>.
/// </summary>
/// <remarks>
/// Under the bearer transport the stored refresh token is sent, so the server revokes its family. Under the cookie
/// transport <see cref="ITokenStorage"/> holds nothing; the server acts on the session cookie alone and ignores the
/// body, which still has to carry a non-empty token because the request model requires one. The order matters
/// there: the antiforgery token is bound to the identity that minted it, so the anonymous replacement that
/// <see cref="BlazorBaseUserAuthStateProvider.MarkUserAsLoggedOut"/> reads must be fetched after the cookie is gone.
/// A logout request that fails on the network still signs the client out locally.
/// </remarks>
public sealed class UserSignOutService(
    IAuthService authService,
    ITokenStorage tokenStorage,
    BlazorBaseUserAuthStateProvider authStateProvider) : IUserSignOutService
{
    #region Injects

    private readonly IAuthService AuthService = authService;
    private readonly ITokenStorage TokenStorage = tokenStorage;
    private readonly BlazorBaseUserAuthStateProvider AuthStateProvider = authStateProvider;

    #endregion

    /// <summary>The body token sent when no refresh token is held; the server ignores it under the cookie transport.</summary>
    public const string CookieSessionPlaceholderToken = "cookie-session";

    public async Task SignOutAsync()
    {
        var tokens = await TokenStorage.GetTokensAsync();
        var refreshToken = String.IsNullOrEmpty(tokens?.RefreshToken) ? CookieSessionPlaceholderToken : tokens.RefreshToken;

        try
        {
            await AuthService.LogoutAsync(refreshToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
        }

        await TokenStorage.ClearTokensAsync();
        await AuthStateProvider.MarkUserAsLoggedOut();
    }
}
