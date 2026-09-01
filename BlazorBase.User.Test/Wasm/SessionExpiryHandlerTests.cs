using System.Net;
using System.Security.Claims;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using Xunit;

namespace BlazorBase.User.Test.Wasm;

/// <summary>
/// Without this handler a session that has expired or been revoked server-side leaves the provider
/// reporting its last cached "signed in" principal while every request keeps failing — the UI shows a
/// broken app instead of dropping to the login screen.
/// </summary>
public sealed class SessionExpiryHandlerTests
{
    private sealed class StubAuthService(AuthSessionResponse session) : IAuthService
    {
        public Task<AuthSessionResponse> GetSessionAsync() => Task.FromResult(session);

        public Task<AuthStatusResponse> GetStatusAsync() => throw new NotSupportedException();
        public Task<LoginResponse> SetupAsync(SetupRequest request) => throw new NotSupportedException();
        public Task<AuthServiceLoginOutcome> LoginAsync(LoginRequest request) => throw new NotSupportedException();
        public Task<LoginResponse> LoginTwoFactorAsync(TwoFactorLoginRequest request) => throw new NotSupportedException();
        public Task<LoginResponse> RefreshAsync(string refreshToken) => throw new NotSupportedException();
        public Task LogoutAsync(string refreshToken) => throw new NotSupportedException();
        public Task<LoginResponse?> DevelopmentLoginAsync() => throw new NotSupportedException();
        public Task ConfirmEmailAsync(ConfirmEmailRequest request) => throw new NotSupportedException();
        public Task ResendConfirmationAsync(ResendConfirmationRequest request) => throw new NotSupportedException();
        public Task ForgotPasswordAsync(ForgotPasswordRequest request) => throw new NotSupportedException();
        public Task ResetPasswordAsync(ResetPasswordRequest request) => throw new NotSupportedException();
        public Task<TwoFactorStatusResponse> GetTwoFactorStatusAsync() => throw new NotSupportedException();
        public Task<TwoFactorRecoveryCodesResponse> EnableTwoFactorAsync(TwoFactorEnableRequest request) => throw new NotSupportedException();
        public Task DisableTwoFactorAsync() => throw new NotSupportedException();
        public Task<TwoFactorRecoveryCodesResponse> RegenerateRecoveryCodesAsync() => throw new NotSupportedException();
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

    private static AuthSessionResponse AuthenticatedSession() => new()
    {
        IsAuthenticated = true,
        Claims = [new AuthSessionClaim(ClaimTypes.Name, "Ada")],
    };

    [Fact]
    public async Task A_401_response_drops_the_authentication_state_to_anonymous()
    {
        var authService = new StubAuthService(AuthenticatedSession());
        var provider = new BlazorBaseUserAuthStateProvider(new NoOpTokenStorage(), authService, new CsrfTokenProvider());
        await provider.GetAuthenticationStateAsync();

        using var invoker = new HttpMessageInvoker(new SessionExpiryHandler(provider)
        {
            InnerHandler = new StubHandler(HttpStatusCode.Unauthorized)
        });

        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/notes"), CancellationToken.None);
        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task A_successful_response_leaves_the_authentication_state_untouched()
    {
        var authService = new StubAuthService(AuthenticatedSession());
        var provider = new BlazorBaseUserAuthStateProvider(new NoOpTokenStorage(), authService, new CsrfTokenProvider());
        await provider.GetAuthenticationStateAsync();

        using var invoker = new HttpMessageInvoker(new SessionExpiryHandler(provider)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK)
        });

        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/notes"), CancellationToken.None);
        var state = await provider.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
    }
}
