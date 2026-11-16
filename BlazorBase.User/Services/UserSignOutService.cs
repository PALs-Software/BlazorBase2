namespace BlazorBase.User.Services;

/// <summary>
/// The framework sign-out path: <c>POST api/auth/logout</c> first, then
/// <see cref="BlazorBaseUserAuthStateProvider.MarkUserAsLoggedOut"/>.
/// </summary>
/// <remarks>
/// The transport is told apart by what <see cref="ITokenStorage"/> holds. A stored refresh token means the bearer
/// transport; nothing stored (<see cref="NoOpTokenStorage"/>) means the cookie transport. Under the bearer
/// transport the stored refresh token is sent, so the server revokes its family. Under the cookie transport the request
/// carries no body and the server acts on the session cookie alone. The order matters there: the antiforgery token is
/// bound to the identity that minted it, so the anonymous replacement that <see cref="BlazorBaseUserAuthStateProvider.MarkUserAsLoggedOut"/>
/// reads must be fetched after the cookie is gone.
/// The server must confirm the logout (a 2xx answer, which <see cref="IAuthService.LogoutAsync"/> turns into an
/// exception otherwise). Under the cookie transport an unconfirmed logout leaves the HttpOnly session cookie and the
/// server session alive and the client cannot drop either, so the user stays signed in and the result is
/// <see cref="SignOutResult.Failed"/>; signing out locally anyway would show an anonymous UI over a live session
/// that a reload signs straight back in. Under the bearer transport the refresh token lives on the client, so
/// dropping it ends the client session even when the server could not revoke the token family: the client signs
/// out and the result is <see cref="SignOutResult.SignedOutWithoutServerRevocation"/>.
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

    public async Task<SignOutResult> SignOutAsync()
    {
        var tokens = await TokenStorage.GetTokensAsync();
        var holdsRefreshToken = !String.IsNullOrEmpty(tokens?.RefreshToken);

        var serverConfirmed = await TryLogoutAsync(holdsRefreshToken ? tokens!.RefreshToken : null);

        if (!serverConfirmed && !holdsRefreshToken)
            return SignOutResult.Failed;

        await TokenStorage.ClearTokensAsync();
        await AuthStateProvider.MarkUserAsLoggedOut();

        return serverConfirmed ? SignOutResult.SignedOut : SignOutResult.SignedOutWithoutServerRevocation;
    }

    private async Task<bool> TryLogoutAsync(string? refreshToken)
    {
        try
        {
            await AuthService.LogoutAsync(refreshToken);
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }
}
