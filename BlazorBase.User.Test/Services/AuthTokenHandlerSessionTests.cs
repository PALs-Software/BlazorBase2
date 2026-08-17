using System.Net;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using NSubstitute;
using Xunit;

namespace BlazorBase.User.Test.Services;

/// <summary>
/// Two ways the handler used to end a session that was perfectly valid. Both showed up as "I have to
/// sign in again all the time", and neither is visible in a single-request test.
/// </summary>
public sealed class AuthTokenHandlerSessionTests
{
    /// <summary>
    /// Refresh tokens rotate, and the server treats a second use of a rotated token as theft: it
    /// revokes the whole family, which signs the user out everywhere. Two requests leaving together
    /// with a near-expiry token were enough to trigger that, and a page loading from two endpoints at
    /// once does exactly that.
    /// </summary>
    [Fact]
    public async Task SendAsync_ConcurrentRequestsNearExpiry_RefreshOnlyOnce()
    {
        var stored = NearExpiryTokens();
        var tokenStorage = Substitute.For<ITokenStorage>();
        tokenStorage.GetTokensAsync().Returns(_ => Task.FromResult<LoginResponse?>(stored));

        var refreshCalls = 0;
        var authService = Substitute.For<IAuthService>();
        authService.RefreshAsync(Arg.Any<string>()).Returns(async _ =>
        {
            Interlocked.Increment(ref refreshCalls);
            await Task.Delay(50);
            stored = RenewedTokens();
            return stored;
        });

        var authStateProvider = new BlazorBaseUserAuthStateProvider(tokenStorage);

        using var first = new HttpMessageInvoker(new AuthTokenHandler(tokenStorage, authService, authStateProvider)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK)
        });
        using var second = new HttpMessageInvoker(new AuthTokenHandler(tokenStorage, authService, authStateProvider)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK)
        });

        await Task.WhenAll(
            first.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/episodes"), CancellationToken.None),
            second.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/episodes/continue"), CancellationToken.None));

        Assert.Equal(1, refreshCalls);
    }

    /// <summary>
    /// A refresh that never reached a verdict — no network, DNS, a proxy in the way — says nothing
    /// about the session. Signing out on it meant a phone moving between wifi and mobile data lost
    /// its login for no reason.
    /// </summary>
    [Fact]
    public async Task SendAsync_RefreshFailsWithoutAnAnswer_KeepsTheSession()
    {
        var tokenStorage = TokenStorageWith(NearExpiryTokens());
        var authService = Substitute.For<IAuthService>();
        authService.RefreshAsync(Arg.Any<string>())
            .Returns<Task<LoginResponse>>(_ => throw new HttpRequestException("Kein Netz"));

        var authStateProvider = new BlazorBaseUserAuthStateProvider(tokenStorage);
        using var invoker = new HttpMessageInvoker(new AuthTokenHandler(tokenStorage, authService, authStateProvider)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK)
        });

        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/episodes"), CancellationToken.None);

        await tokenStorage.DidNotReceive().ClearTokensAsync();
    }

    /// <summary>
    /// A refresh token the server actually turns down is a different matter: the session really is
    /// over and the stored tokens have to go, otherwise the app keeps retrying a dead session.
    /// </summary>
    [Fact]
    public async Task SendAsync_RefreshRejectedByServer_EndsTheSession()
    {
        var tokenStorage = TokenStorageWith(NearExpiryTokens());
        var authService = Substitute.For<IAuthService>();
        authService.RefreshAsync(Arg.Any<string>())
            .Returns<Task<LoginResponse>>(_ => throw new HttpRequestException("abgelehnt", null, HttpStatusCode.Unauthorized));

        var authStateProvider = new BlazorBaseUserAuthStateProvider(tokenStorage);
        using var invoker = new HttpMessageInvoker(new AuthTokenHandler(tokenStorage, authService, authStateProvider)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK)
        });

        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/episodes"), CancellationToken.None);

        await tokenStorage.Received().ClearTokensAsync();
    }

    private static LoginResponse NearExpiryTokens() => new()
    {
        AccessToken = "near-expiry",
        RefreshToken = "current-refresh-token",
        ExpiresAt = DateTime.UtcNow.AddSeconds(20)
    };

    private static LoginResponse RenewedTokens() => new()
    {
        AccessToken = "renewed",
        RefreshToken = "renewed-refresh-token",
        ExpiresAt = DateTime.UtcNow.AddHours(1)
    };

    private static ITokenStorage TokenStorageWith(LoginResponse tokens)
    {
        var storage = Substitute.For<ITokenStorage>();
        storage.GetTokensAsync().Returns(Task.FromResult<LoginResponse?>(tokens));
        return storage;
    }

    private sealed class StubHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        #region Injects
        private readonly HttpStatusCode StatusCode = statusCode;
        #endregion

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(StatusCode));
    }
}
