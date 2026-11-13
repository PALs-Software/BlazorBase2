using BlazorBase.User.Models;
using BlazorBase.User.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace BlazorBase.User.Test.Services;

/// <summary>
/// Signing out ends the session on the server first and only then drops the client to anonymous. Under the bearer
/// transport the held refresh token is revoked and a failed revocation still signs the client out (reported as
/// such); under the cookie transport (no token held) a logout the server did not confirm keeps the user signed in.
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

    private void HoldBearerTokens() =>
        TokenStorage.GetTokensAsync().Returns(new LoginResponse { AccessToken = "access", RefreshToken = "refresh-1" });

    private void HoldNoTokens() =>
        TokenStorage.GetTokensAsync().Returns((LoginResponse?)null);

    [Fact]
    public async Task Bearer_RevokesTheHeldRefreshToken_ThenSignsTheClientOut()
    {
        HoldBearerTokens();
        var service = CreateService();

        var result = await service.SignOutAsync();

        Assert.Equal(SignOutResult.SignedOut, result);
        await AuthService.Received(1).LogoutAsync("refresh-1");
        await TokenStorage.Received(1).ClearTokensAsync();
        Assert.Equal(["Logout", "MarkUserAsLoggedOut"], Calls);
    }

    [Fact]
    public async Task Cookie_PostsTheLogoutWithAPlaceholderToken_ThenSignsTheClientOut()
    {
        HoldNoTokens();
        var service = CreateService();

        var result = await service.SignOutAsync();

        Assert.Equal(SignOutResult.SignedOut, result);
        await AuthService.Received(1).LogoutAsync(UserSignOutService.CookieSessionPlaceholderToken);
        Assert.Equal(["Logout", "MarkUserAsLoggedOut"], Calls);
    }

    [Fact]
    public async Task Cookie_NetworkFailure_KeepsTheUserSignedIn()
    {
        HoldNoTokens();
        var service = CreateService();
        AuthService.LogoutAsync(Arg.Any<string>()).ThrowsAsync(new HttpRequestException("offline"));

        var result = await service.SignOutAsync();

        Assert.Equal(SignOutResult.Failed, result);
        await TokenStorage.DidNotReceive().ClearTokensAsync();
        Assert.Empty(Calls);
    }

    [Fact]
    public async Task Cookie_Timeout_KeepsTheUserSignedIn()
    {
        HoldNoTokens();
        var service = CreateService();
        AuthService.LogoutAsync(Arg.Any<string>()).ThrowsAsync(new TaskCanceledException("timeout"));

        var result = await service.SignOutAsync();

        Assert.Equal(SignOutResult.Failed, result);
        Assert.Empty(Calls);
    }

    [Theory]
    [InlineData(System.Net.HttpStatusCode.Forbidden)]
    [InlineData(System.Net.HttpStatusCode.TooManyRequests)]
    [InlineData(System.Net.HttpStatusCode.InternalServerError)]
    public async Task Cookie_NonSuccessStatus_KeepsTheUserSignedIn(System.Net.HttpStatusCode statusCode)
    {
        HoldNoTokens();
        var service = CreateService();
        AuthService.LogoutAsync(Arg.Any<string>()).ThrowsAsync(new HttpRequestException("rejected", null, statusCode));

        var result = await service.SignOutAsync();

        Assert.Equal(SignOutResult.Failed, result);
        await TokenStorage.DidNotReceive().ClearTokensAsync();
        Assert.Empty(Calls);
    }

    [Fact]
    public async Task Bearer_NetworkFailure_StillSignsTheClientOutAndReportsTheMissingRevocation()
    {
        HoldBearerTokens();
        var service = CreateService();
        AuthService.LogoutAsync(Arg.Any<string>()).ThrowsAsync(new HttpRequestException("offline"));

        var result = await service.SignOutAsync();

        Assert.Equal(SignOutResult.SignedOutWithoutServerRevocation, result);
        await TokenStorage.Received(1).ClearTokensAsync();
        Assert.Equal(["MarkUserAsLoggedOut"], Calls);
    }

    [Fact]
    public async Task Bearer_NonSuccessStatus_StillSignsTheClientOutAndReportsTheMissingRevocation()
    {
        HoldBearerTokens();
        var service = CreateService();
        AuthService.LogoutAsync(Arg.Any<string>())
            .ThrowsAsync(new HttpRequestException("rejected", null, System.Net.HttpStatusCode.InternalServerError));

        var result = await service.SignOutAsync();

        Assert.Equal(SignOutResult.SignedOutWithoutServerRevocation, result);
        await TokenStorage.Received(1).ClearTokensAsync();
        Assert.Equal(["MarkUserAsLoggedOut"], Calls);
    }
}
