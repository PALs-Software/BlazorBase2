using System.Security.Claims;
using BlazorBase.User.Models;

namespace BlazorBase.User.Services;

/// <summary>
/// Authentication state for the cookie transport: the claims come from the server, because the token
/// they live in is <c>HttpOnly</c> and the client cannot read it.
/// </summary>
/// <remarks>
/// Derives from <see cref="BlazorBaseUserAuthStateProvider"/> rather than replacing it, so the UI
/// components that depend on the concrete type keep working. Only the claim source changes.
///
/// The result is cached for the lifetime of the scope. This is a display concern — which menu entries
/// to render, which route to guard — and it is never the authorization decision: the server re-checks
/// every request regardless of what the client believes about itself.
/// </remarks>
public class CookieAuthStateProvider(IAuthService authService, ITokenStorage tokenStorage)
    : BlazorBaseUserAuthStateProvider(tokenStorage)
{
    #region Injects
    private readonly IAuthService AuthService = authService;
    #endregion

    private ClaimsPrincipal? _cachedPrincipal;

    protected override async Task<ClaimsPrincipal> ResolvePrincipalAsync()
    {
        if (_cachedPrincipal is not null)
            return _cachedPrincipal;

        var session = await AuthService.GetSessionAsync();

        _cachedPrincipal = session.IsAuthenticated
            ? new ClaimsPrincipal(new ClaimsIdentity(
                session.Claims.Select(claim => new Claim(claim.Type, claim.Value)),
                authenticationType: "cookie",
                nameType: ClaimTypes.Name,
                roleType: ClaimTypes.Role))
            : new ClaimsPrincipal(new ClaimsIdentity());

        return _cachedPrincipal;
    }

    /// <summary>
    /// The token argument carries no values under this transport — the server put them in cookies —
    /// so the state is re-read from the server instead of parsed out of it.
    /// </summary>
    public override async Task MarkUserAsAuthenticated(LoginResponse tokens)
    {
        _cachedPrincipal = null;
        CurrentUser = await ResolvePrincipalAsync();

        NotifyCurrentUserChanged();
    }

    public override Task MarkUserAsLoggedOut()
    {
        // Cached as a known-anonymous principal rather than cleared: clearing would only mark the
        // state unknown, and the next state query would ask the server again and re-authenticate from
        // a session the user just ended.
        _cachedPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        CurrentUser = _cachedPrincipal;

        NotifyCurrentUserChanged();
        return Task.CompletedTask;
    }
}
