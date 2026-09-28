using System.Security.Claims;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using Xunit;

namespace BlazorBase.User.Test.Services;

/// <summary>
/// The claims always come from the server: an access token is now an opaque Identity bearer token
/// (or, under the cookie transport, not readable by the client at all), so the provider never
/// attempts to parse one. These cover that the principal is built from the session answer alone —
/// including every role, which a profile endpoint carrying a single role string would have lost.
/// </summary>
public sealed class BlazorBaseUserAuthStateProviderTests
{
    private sealed class StubAuthService(AuthSessionResponse session) : IAuthService
    {
        public int SessionCallCount { get; private set; }

        public string? LastAccessToken { get; private set; }

        public Task<AuthSessionResponse> GetSessionAsync(string? accessToken = null)
        {
            SessionCallCount++;
            LastAccessToken = accessToken;
            return Task.FromResult(session);
        }

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
        public Task DisableTwoFactorAsync(TwoFactorVerificationRequest request) => throw new NotSupportedException();
        public Task<TwoFactorRecoveryCodesResponse> RegenerateRecoveryCodesAsync(TwoFactorVerificationRequest request) => throw new NotSupportedException();
    }

    private static AuthSessionResponse AuthenticatedSession() => new()
    {
        IsAuthenticated = true,
        Claims =
        [
            new AuthSessionClaim(ClaimTypes.NameIdentifier, "user-1"),
            new AuthSessionClaim(ClaimTypes.Name, "Ada"),
            new AuthSessionClaim(ClaimTypes.Role, "Admin"),
            new AuthSessionClaim(ClaimTypes.Role, "Auditor"),
        ],
    };

    private static BlazorBaseUserAuthStateProvider CreateProvider(AuthSessionResponse session, ITokenStorage? tokenStorage = null)
        => new(tokenStorage ?? new NoOpTokenStorage(), new StubAuthService(session), new CsrfTokenProvider());

    [Fact]
    public async Task The_principal_is_built_from_the_servers_answer()
    {
        var provider = CreateProvider(AuthenticatedSession());

        var state = await provider.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal("Ada", state.User.Identity?.Name);
    }

    /// <summary>
    /// A profile endpoint exposing one role string would silently drop the rest, and every role-gated
    /// piece of UI would disappear for a multi-role user.
    /// </summary>
    [Fact]
    public async Task Every_role_survives_not_just_the_first()
    {
        var provider = CreateProvider(AuthenticatedSession());

        var state = await provider.GetAuthenticationStateAsync();

        Assert.True(state.User.IsInRole("Admin"));
        Assert.True(state.User.IsInRole("Auditor"));
    }

    [Fact]
    public async Task An_unauthenticated_session_yields_an_anonymous_principal()
    {
        var provider = CreateProvider(new AuthSessionResponse());

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    /// <summary>
    /// The session endpoint is the only source of the claims, and under the bearer transport nothing
    /// else identifies the caller to it: the client this call goes through carries no
    /// <see cref="AuthTokenHandler"/> on purpose, so the provider has to attach the token itself or
    /// the endpoint answers anonymous however successful the login was.
    /// </summary>
    [Fact]
    public async Task The_stored_access_token_is_handed_to_the_session_call()
    {
        var authService = new StubAuthService(AuthenticatedSession());
        var tokenStorage = new InMemoryTestTokenStorage();
        await tokenStorage.SaveTokensAsync(new LoginResponse { AccessToken = "stored-access-token" });
        var provider = new BlazorBaseUserAuthStateProvider(tokenStorage, authService, new CsrfTokenProvider());

        await provider.GetAuthenticationStateAsync();

        Assert.Equal("stored-access-token", authService.LastAccessToken);
    }

    /// <summary>
    /// Under the cookie transport <see cref="NoOpTokenStorage"/> holds nothing and the session cookie
    /// identifies the caller, so no header may be invented.
    /// </summary>
    [Fact]
    public async Task No_access_token_is_sent_when_none_is_stored()
    {
        var authService = new StubAuthService(AuthenticatedSession());
        var provider = new BlazorBaseUserAuthStateProvider(new NoOpTokenStorage(), authService, new CsrfTokenProvider());

        await provider.GetAuthenticationStateAsync();

        Assert.Null(authService.LastAccessToken);
    }

    private sealed class InMemoryTestTokenStorage : ITokenStorage
    {
        private LoginResponse? Tokens;

        public Task<LoginResponse?> GetTokensAsync() => Task.FromResult(Tokens);

        public Task SaveTokensAsync(LoginResponse tokens)
        {
            Tokens = tokens;
            return Task.CompletedTask;
        }

        public Task ClearTokensAsync()
        {
            Tokens = null;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task The_server_is_asked_once_per_scope_not_once_per_render()
    {
        var authService = new StubAuthService(AuthenticatedSession());
        var provider = new BlazorBaseUserAuthStateProvider(new NoOpTokenStorage(), authService, new CsrfTokenProvider());

        await provider.GetAuthenticationStateAsync();
        await provider.GetAuthenticationStateAsync();
        await provider.GetAuthenticationStateAsync();

        Assert.Equal(1, authService.SessionCallCount);
    }

    [Fact]
    public async Task Logging_out_drops_the_cached_principal()
    {
        var provider = CreateProvider(AuthenticatedSession());
        await provider.GetAuthenticationStateAsync();

        await provider.MarkUserAsLoggedOut();
        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    /// <summary>
    /// A bearer access token is now an opaque Identity token, not a JWT — this stores one that is not
    /// even shaped like one and confirms the provider still resolves the claims from the session
    /// answer instead of trying, and failing, to decode the token.
    /// </summary>
    [Fact]
    public async Task An_opaque_non_jwt_access_token_still_yields_the_servers_claims()
    {
        var tokenStorage = new NoOpTokenStorage();
        await tokenStorage.SaveTokensAsync(new LoginResponse { AccessToken = "not-a-jwt-opaque-token", RefreshToken = "r" });
        var provider = CreateProvider(AuthenticatedSession(), tokenStorage);

        var state = await provider.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal("Ada", state.User.Identity?.Name);
    }

    [Fact]
    public async Task Invalidate_drops_the_cached_principal_without_asking_the_server_again()
    {
        var authService = new StubAuthService(AuthenticatedSession());
        var provider = new BlazorBaseUserAuthStateProvider(new NoOpTokenStorage(), authService, new CsrfTokenProvider());
        await provider.GetAuthenticationStateAsync();

        await provider.InvalidateAsync();
        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
        Assert.Equal(1, authService.SessionCallCount);
    }
}
