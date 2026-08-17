using System.Net;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using NSubstitute;
using Xunit;

namespace BlazorBase.User.Test.Services;

/// <summary>
/// Regression test for USER-01: <see cref="AuthTokenHandler"/> must not proactively refresh (and
/// thereby rotate) the refresh token when the outgoing request targets the logout endpoint.
/// Otherwise the logout request body revokes a refresh token that is no longer the one the server
/// considers current, and the freshly-rotated token stays valid, leaving the user not fully logged out.
/// </summary>
public sealed class AuthTokenHandlerLogoutExemptionTests
{
    [Theory]
    [InlineData("https://localhost/api/auth/logout", false)]
    [InlineData("https://localhost/api/notes", true)]
    public async Task SendAsync_NearExpiryAccessToken_RefreshesOnlyForNonAuthEndpoints(
        string requestUri, bool expectRefresh)
    {
        var currentTokens = new LoginResponse
        {
            AccessToken = "near-expiry-access-token",
            RefreshToken = "current-refresh-token",
            ExpiresAt = DateTime.UtcNow.AddSeconds(30),
        };

        var tokenStorage = Substitute.For<ITokenStorage>();
        tokenStorage.GetTokensAsync().Returns(Task.FromResult<LoginResponse?>(currentTokens));

        var authService = Substitute.For<IAuthService>();
        authService.RefreshAsync(currentTokens.RefreshToken).Returns(Task.FromResult(new LoginResponse
        {
            AccessToken = "refreshed-access-token",
            RefreshToken = "refreshed-refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
        }));

        var authStateProvider = new BlazorBaseUserAuthStateProvider(tokenStorage);

        var handler = new AuthTokenHandler(tokenStorage, authService, authStateProvider)
        {
            InnerHandler = new StubHttpMessageHandler(HttpStatusCode.OK),
        };

        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);

        await invoker.SendAsync(request, CancellationToken.None);

        if (expectRefresh)
            await authService.Received(1).RefreshAsync(currentTokens.RefreshToken);
        else
            await authService.DidNotReceive().RefreshAsync(Arg.Any<string>());
    }

    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        #region Injects
        private readonly HttpStatusCode StatusCode = statusCode;
        #endregion

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(StatusCode));
    }
}
