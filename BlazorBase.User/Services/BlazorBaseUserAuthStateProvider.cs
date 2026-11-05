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
/// header is attached to that call. The result is cached until the next sign-in or sign-out; a sign-out
/// or an expired session caches an anonymous principal without asking the server, and replaces the
/// antiforgery token with an anonymous one when a token was held, so the login form keeps working. This is a
/// display concern — which menu entries to render, which route to guard — and never the authorization
/// decision: the server re-checks every request regardless of what the client believes about itself.
///
/// The cache and its change notification live in <see cref="AuthenticationSessionState"/>, not in this
/// instance. IHttpClientFactory resolves the providers used by <see cref="AuthTokenHandler"/> and
/// <see cref="SessionExpiryHandler"/> in scopes of their own, and only shared state lets a 401 seen by
/// one of them reach the provider the components render with.
/// </remarks>
public class BlazorBaseUserAuthStateProvider : AuthenticationStateProvider, IDisposable
{
    #region Injects
    private readonly ITokenStorage TokenStorage;
    private readonly IAuthService AuthService;
    private readonly CsrfTokenProvider CsrfTokenProvider;
    private readonly AuthenticationSessionState SessionState;
    #endregion

    public BlazorBaseUserAuthStateProvider(
        ITokenStorage tokenStorage,
        IAuthService authService,
        CsrfTokenProvider csrfTokenProvider,
        AuthenticationSessionState sessionState)
    {
        TokenStorage = tokenStorage;
        AuthService = authService;
        CsrfTokenProvider = csrfTokenProvider;
        SessionState = sessionState;
        SessionState.Changed += OnSessionChanged;
    }

    /// <summary>
    /// A provider with session state of its own, shared with nobody. Enough for a test or a host that
    /// resolves every consumer from one scope; dependency injection picks the constructor taking an
    /// <see cref="AuthenticationSessionState"/> whenever one is registered.
    /// </summary>
    public BlazorBaseUserAuthStateProvider(ITokenStorage tokenStorage, IAuthService authService, CsrfTokenProvider csrfTokenProvider)
        : this(tokenStorage, authService, csrfTokenProvider, new AuthenticationSessionState())
    {
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync() =>
        new(await ResolvePrincipalAsync());

    private async Task<ClaimsPrincipal> ResolvePrincipalAsync()
    {
        if (SessionState.Principal is { } cachedPrincipal)
            return cachedPrincipal;

        var session = await AuthService.GetSessionAsync(await ReadAccessTokenAsync());
        CsrfTokenProvider.Set(session.CsrfToken);

        var principal = session.IsAuthenticated
            ? new ClaimsPrincipal(new ClaimsIdentity(
                session.Claims.Select(claim => new Claim(claim.Type, claim.Value)),
                authenticationType: "server",
                nameType: ClaimTypes.Name,
                roleType: ClaimTypes.Role))
            : new ClaimsPrincipal(new ClaimsIdentity());

        SessionState.Remember(principal);
        return principal;
    }

    /// <summary>
    /// Null under the cookie transport, where <see cref="ITokenStorage"/> is <c>NoOpTokenStorage</c>
    /// and the session cookie identifies the caller on its own. Under the bearer transport this is
    /// the only thing that identifies the caller to the session endpoint, because the client this
    /// service calls through deliberately carries no <see cref="AuthTokenHandler"/>.
    /// </summary>
    private async Task<string?> ReadAccessTokenAsync()
    {
        var tokens = await TokenStorage.GetTokensAsync();
        return tokens?.AccessToken;
    }

    /// <summary>
    /// Stores the tokens (a no-op under the cookie transport, where <see cref="ITokenStorage"/> is
    /// <c>NoOpTokenStorage</c>) and re-reads the session, rather than trusting anything the login
    /// response itself carries — the server, not the token, is the source of the claims.
    /// </summary>
    public virtual async Task MarkUserAsAuthenticated(LoginResponse tokens)
    {
        await TokenStorage.SaveTokensAsync(tokens);
        SessionState.Forget();
        SessionState.Announce(await ResolvePrincipalAsync());
    }

    public virtual Task MarkUserAsLoggedOut() => InvalidateAsync();

    /// <summary>
    /// Drops the cached session and reports the caller as anonymous without taking the principal from
    /// the server. Called after an explicit logout, and by <see cref="SessionExpiryHandler"/> when the
    /// API answers 401 — a cached "signed in" principal next to every request being refused is a dead
    /// end the UI can only show as a broken app, so the state has to drop to the login screen instead.
    /// </summary>
    /// <remarks>
    /// When an antiforgery token was held (the cookie transport), it is replaced by the anonymous one
    /// the session endpoint hands out, because the held token is bound to the identity that just ended
    /// and the login request is a write that the server refuses without a valid token. Nothing else
    /// reads the session again once the anonymous principal is cached, so without this the login form
    /// could not succeed before a page reload. A host that signs out has to call the server-side logout
    /// first and this method afterwards: the replacement token is minted for the identity of the
    /// request that fetches it, and it only validates once the session cookie is gone. With no
    /// antiforgery token held (the bearer transport, MAUI) the server is not asked at all. The
    /// anonymous principal is announced even when the session read throws; the exception still
    /// propagates, and no antiforgery token is held afterwards.
    /// </remarks>
    public async Task InvalidateAsync()
    {
        await TokenStorage.ClearTokensAsync();

        try
        {
            await ReplaceAntiforgeryTokenAsync();
        }
        finally
        {
            SessionState.Announce(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    /// <summary>
    /// Clears the held antiforgery token before asking for a new one, so a failed read never leaves the
    /// stale, identity-bound token behind. Does nothing when no token is held.
    /// </summary>
    private async Task ReplaceAntiforgeryTokenAsync()
    {
        if (CsrfTokenProvider.Token is null)
            return;

        CsrfTokenProvider.Clear();
        var session = await AuthService.GetSessionAsync();
        CsrfTokenProvider.Set(session.CsrfToken);
    }

    private void OnSessionChanged(ClaimsPrincipal principal) =>
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(principal)));

    public void Dispose()
    {
        SessionState.Changed -= OnSessionChanged;
        GC.SuppressFinalize(this);
    }
}
