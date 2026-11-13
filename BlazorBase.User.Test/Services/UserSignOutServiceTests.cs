using BlazorBase.User.Models;
using BlazorBase.User.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace BlazorBase.User.Test.Services;

/// <summary>
/// Signing out ends the session on the server first and only then drops the client to anonymous, under both
/// transports: the bearer transport revokes the held refresh token, the cookie transport (no token held) still
/// posts the logout so the server clears its session cookie.
/// </summary>
public sealed class UserSignOutServiceTests
{
    private sealed class RecordingAuthStateProvider(ITokenStorage tokenStorage, IAuthService authService, List<string> calls)
        : BlazorBaseUserAuthStateProvider(tokenStorage, authService, new CsrfTokenProvider())
    {
        #region Injects
        private readonly List<string> Calls = calls;
        #endregion

        public override Task MarkUserAsLoggedOut()
        {
            Calls.Add("MarkUserAsLoggedOut");
            return Task.CompletedTask;
        }
    }

    private readonly List<string> Calls = [];
    private readonly IAuthService AuthService = Substitute.For<IAuthService>();
    private readonly ITokenStorage TokenStorage = Substitute.For<ITokenStorage>();

    private UserSignOutService CreateService()
    {
        AuthService.LogoutAsync(Arg.Any<string>()).Returns(_ =>
        {
            Calls.Add("Logout");
            return Task.CompletedTask;
        });

        return new UserSignOutService(AuthService, TokenStorage, new RecordingAuthStateProvider(TokenStorage, AuthService, Calls));
    }

    [Fact]
    public async Task Bearer_RevokesTheHeldRefreshToken_ThenSignsTheClientOut()
    {
        TokenStorage.GetTokensAsync().Returns(new LoginResponse { AccessToken = "access", RefreshToken = "refresh-1" });
        var service = CreateService();

        await service.SignOutAsync();

        await AuthService.Received(1).LogoutAsync("refresh-1");
        await TokenStorage.Received(1).ClearTokensAsync();
        Assert.Equal(["Logout", "MarkUserAsLoggedOut"], Calls);
    }

    [Fact]
    public async Task Cookie_PostsTheLogoutWithAPlaceholderToken_ThenSignsTheClientOut()
    {
        TokenStorage.GetTokensAsync().Returns((LoginResponse?)null);
        var service = CreateService();

        await service.SignOutAsync();

        await AuthService.Received(1).LogoutAsync(UserSignOutService.CookieSessionPlaceholderToken);
        Assert.Equal(["Logout", "MarkUserAsLoggedOut"], Calls);
    }

    [Fact]
    public async Task UnreachableServer_StillSignsTheClientOut()
    {
        TokenStorage.GetTokensAsync().Returns(new LoginResponse { RefreshToken = "refresh-1" });
        var service = CreateService();
        AuthService.LogoutAsync(Arg.Any<string>()).ThrowsAsync(new HttpRequestException("offline"));

        await service.SignOutAsync();

        await TokenStorage.Received(1).ClearTokensAsync();
        Assert.Equal(["MarkUserAsLoggedOut"], Calls);
    }
}
