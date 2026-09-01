using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using BlazorBase.User.Models;

namespace BlazorBase.User.Services;

/// <summary>
/// Derives the authentication state for both Identity transports.
/// </summary>
/// <remarks>
/// Access tokens are opaque Identity bearer tokens the client cannot decode, so the claims always
/// come from <c>GET /api/auth/session</c> — <see cref="ITokenStorage"/> only decides whether a bearer
/// header is attached to that call. The result is cached for the lifetime of the scope. This is a
/// display concern — which menu entries to render, which route to guard — and never the authorization
/// decision: the server re-checks every request regardless of what the client believes about itself.
/// </remarks>
public class BlazorBaseUserAuthStateProvider(
    ITokenStorage tokenStorage,
    IAuthService authService,
    CsrfTokenProvider csrfTokenProvider) : AuthenticationStateProvider
{
    #region Injects
    private readonly ITokenStorage TokenStorage = tokenStorage;
    private readonly IAuthService AuthService = authService;
    private readonly CsrfTokenProvider CsrfTokenProvider = csrfTokenProvider;
    #endregion

    private ClaimsPrincipal CurrentUser = new(new ClaimsIdentity());
    private ClaimsPrincipal? _cachedPrincipal;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        CurrentUser = await ResolvePrincipalAsync();
        return new AuthenticationState(CurrentUser);
    }

    private async Task<ClaimsPrincipal> ResolvePrincipalAsync()
    {
        if (_cachedPrincipal is not null)
            return _cachedPrincipal;

        var session = await AuthService.GetSessionAsync();
        CsrfTokenProvider.Set(session.CsrfToken);

        _cachedPrincipal = session.IsAuthenticated
            ? new ClaimsPrincipal(new ClaimsIdentity(
                session.Claims.Select(claim => new Claim(claim.Type, claim.Value)),
                authenticationType: "server",
                nameType: ClaimTypes.Name,
                roleType: ClaimTypes.Role))
            : new ClaimsPrincipal(new ClaimsIdentity());

        return _cachedPrincipal;
    }

    /// <summary>
    /// Stores the tokens (a no-op under the cookie transport, where <see cref="ITokenStorage"/> is
    /// <c>NoOpTokenStorage</c>) and re-reads the session, rather than trusting anything the login
    /// response itself carries — the server, not the token, is the source of the claims.
    /// </summary>
    public virtual async Task MarkUserAsAuthenticated(LoginResponse tokens)
    {
        await TokenStorage.SaveTokensAsync(tokens);
        _cachedPrincipal = null;
        CurrentUser = await ResolvePrincipalAsync();

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(CurrentUser)));
    }

    public virtual Task MarkUserAsLoggedOut() => InvalidateAsync();

    /// <summary>
    /// Drops the cached session and reports the caller as anonymous without asking the server again.
    /// Called after an explicit logout, and by <see cref="SessionExpiryHandler"/> when the API answers
    /// 401 — a cached "signed in" principal next to every request being refused is a dead end the UI
    /// can only show as a broken app, so the state has to drop to the login screen instead.
    /// </summary>
    public async Task InvalidateAsync()
    {
        await TokenStorage.ClearTokensAsync();
        _cachedPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        CurrentUser = _cachedPrincipal;
        CsrfTokenProvider.Clear();

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(CurrentUser)));
    }
}
