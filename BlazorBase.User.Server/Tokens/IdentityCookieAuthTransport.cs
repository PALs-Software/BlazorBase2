using System.Security.Claims;
using BlazorBase.User.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Signs the caller into <see cref="IdentityConstants.ApplicationScheme"/> instead of handing back a
/// token, and blanks the token pair out of the response body — the backend-for-frontend shape, for a
/// browser client served from the same origin as the API.
/// </summary>
/// <remarks>
/// This replaces a self-issued JWT that <c>Response.Cookies.Append</c> wrote into a cookie by hand.
/// That approach had no refresh path (the cookie died with the token, uncontactable again until the
/// browser session ended), no revocation (a deactivated user or a rotated role stayed valid until the
/// token expired), and no chunking (past 4096 bytes a browser silently drops the cookie). The
/// platform's own cookie handler does all three: <c>SlidingExpiration</c> renews the cookie on every
/// request, <c>SecurityStampValidator</c> re-checks the user's security stamp on the schedule
/// configured for it, and the handler chunks a cookie that outgrows a single one automatically.
///
/// Signs in through <see cref="HttpContext.SignInAsync(string, ClaimsPrincipal)"/> directly rather
/// than <c>SignInManager&lt;TUser&gt;.SignInAsync</c>: this class, and every place that registers it,
/// is resolved without a <c>TUser</c> type parameter, so it never has a concrete
/// <c>SignInManager&lt;TUser&gt;</c> to call. <paramref name="principal"/> already is the same
/// principal <c>SignInManager&lt;TUser&gt;.SignInAsync</c> would build and sign in with, because the
/// caller obtains it from <c>SignInManager&lt;TUser&gt;.CreateUserPrincipalAsync</c> before calling
/// <see cref="WriteAsync"/>.
///
/// Cookies bring back CSRF, which bearer tokens do not have. <c>AddBlazorBaseCookieAuthTokens</c>
/// requires the host to call <c>app.UseBlazorBaseCsrf()</c> for exactly that reason — see
/// <see cref="BlazorBaseCsrfMiddleware"/>.
/// </remarks>
public sealed class IdentityCookieAuthTransport(IAntiforgery antiforgery) : IAuthTokenTransport
{
    #region Injects
    private readonly IAntiforgery Antiforgery = antiforgery;
    #endregion

    public async Task<LoginResponse> WriteAsync(HttpContext httpContext, LoginResponse tokens, ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        await httpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal, new AuthenticationProperties { IsPersistent = true });

        return new LoginResponse
        {
            AccessToken  = string.Empty,
            RefreshToken = string.Empty,
            ExpiresAt    = tokens.ExpiresAt,
        };
    }

    /// <summary>
    /// Always <see langword="null"/>. The session lives entirely in the Identity cookie; there is no
    /// separate refresh token to rotate, so a caller that still asks for one gets nothing to present.
    /// </summary>
    public Task<string?> ReadRefreshTokenAsync(HttpContext httpContext, string? fromRequestBody, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public async Task ClearAsync(HttpContext httpContext, CancellationToken cancellationToken = default) =>
        await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

    /// <summary>
    /// Requires a same-origin fetch and a valid antiforgery token. Without this, a cross-site page
    /// could rotate or revoke a visitor's session simply by causing the request — the browser would
    /// attach the cookie for it.
    /// </summary>
    public async Task<bool> IsRequestIntentionalAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        if (!BlazorBaseCsrfChecks.IsSameOriginRequest(httpContext))
            return false;

        return await Antiforgery.IsRequestValidAsync(httpContext);
    }
}
