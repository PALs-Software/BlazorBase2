using System.Security.Claims;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using Xunit;

namespace BlazorBase.User.Test.Services;

/// <summary>
/// Under the cookie transport the client cannot read its own token, so the claims come from the
/// server. These cover that the principal is built from that answer — including every role, which a
/// profile endpoint carrying a single role string would have lost.
/// </summary>
public sealed class CookieAuthStateProviderTests
{
    private sealed class StubAuthService(AuthSessionResponse session) : IAuthService
    {
        public int SessionCallCount { get; private set; }

        public Task<AuthSessionResponse> GetSessionAsync()
        {
            SessionCallCount++;
            return Task.FromResult(session);
        }

        public Task<AuthStatusResponse> GetStatusAsync() => throw new NotSupportedException();
        public Task<LoginResponse> SetupAsync(SetupRequest request) => throw new NotSupportedException();
        public Task<LoginResponse> LoginAsync(LoginRequest request) => throw new NotSupportedException();
        public Task<LoginResponse> RefreshAsync(string refreshToken) => throw new NotSupportedException();
        public Task LogoutAsync(string refreshToken) => throw new NotSupportedException();
        public Task<LoginResponse?> DevelopmentLoginAsync() => throw new NotSupportedException();
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

    [Fact]
    public async Task The_principal_is_built_from_the_servers_answer()
    {
        var provider = new CookieAuthStateProvider(new StubAuthService(AuthenticatedSession()), new NoOpTokenStorage());

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
        var provider = new CookieAuthStateProvider(new StubAuthService(AuthenticatedSession()), new NoOpTokenStorage());

        var state = await provider.GetAuthenticationStateAsync();

        Assert.True(state.User.IsInRole("Admin"));
        Assert.True(state.User.IsInRole("Auditor"));
    }

    [Fact]
    public async Task An_unauthenticated_session_yields_an_anonymous_principal()
    {
        var provider = new CookieAuthStateProvider(new StubAuthService(new AuthSessionResponse()), new NoOpTokenStorage());

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task The_server_is_asked_once_per_scope_not_once_per_render()
    {
        var authService = new StubAuthService(AuthenticatedSession());
        var provider = new CookieAuthStateProvider(authService, new NoOpTokenStorage());

        await provider.GetAuthenticationStateAsync();
        await provider.GetAuthenticationStateAsync();
        await provider.GetAuthenticationStateAsync();

        Assert.Equal(1, authService.SessionCallCount);
    }

    [Fact]
    public async Task Logging_out_drops_the_cached_principal()
    {
        var provider = new CookieAuthStateProvider(new StubAuthService(AuthenticatedSession()), new NoOpTokenStorage());
        await provider.GetAuthenticationStateAsync();

        await provider.MarkUserAsLoggedOut();
        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }
}
