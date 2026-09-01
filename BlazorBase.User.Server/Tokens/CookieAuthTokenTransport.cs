using BlazorBase.User.Models;
using BlazorBase.User.Server.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Delivers the token pair as <c>HttpOnly</c> cookies and blanks it out of the response body — the
/// backend-for-frontend shape, for a browser client served from the same origin as the API.
/// </summary>
/// <remarks>
/// The point is narrow and worth stating precisely: an <c>HttpOnly</c> cookie stops an XSS from
/// <em>stealing</em> the token and replaying it elsewhere or later. It does not stop that script from
/// making authenticated requests from the victim's browser while the session lasts. The gain is
/// against exfiltration and persistence, not against abuse in the moment — real, but not a licence to
/// stop worrying about XSS.
///
/// Cookies bring back CSRF, which bearer tokens do not have. Pair this transport with
/// <c>RequireBlazorBaseCsrf()</c> on every state-changing endpoint.
/// </remarks>
public sealed class CookieAuthTokenTransport(
    IOptions<AuthCookieOptions> options,
    IConfiguration configuration,
    IAntiforgery antiforgery) : IAuthTokenTransport
{
    #region Injects
    private readonly AuthCookieOptions Options = options.Value;
    private readonly IConfiguration Configuration = configuration;
    private readonly IAntiforgery Antiforgery = antiforgery;
    #endregion

    public Task<LoginResponse> WriteAsync(HttpContext httpContext, LoginResponse tokens, CancellationToken cancellationToken = default)
    {
        httpContext.Response.Cookies.Append(Options.AccessTokenCookieName, tokens.AccessToken, BuildAccessCookie(tokens.ExpiresAt));
        httpContext.Response.Cookies.Append(Options.RefreshTokenCookieName, tokens.RefreshToken, BuildRefreshCookie());

        return Task.FromResult(new LoginResponse
        {
            AccessToken  = string.Empty,
            RefreshToken = string.Empty,
            ExpiresAt    = tokens.ExpiresAt,
        });
    }

    /// <summary>
    /// Reads the refresh token from its cookie and ignores <paramref name="fromRequestBody"/>
    /// entirely. Honouring a body value would let a caller present a token the browser never held,
    /// which is the hole this transport exists to close.
    /// </summary>
    public Task<string?> ReadRefreshTokenAsync(HttpContext httpContext, string? fromRequestBody, CancellationToken cancellationToken = default)
    {
        httpContext.Request.Cookies.TryGetValue(Options.RefreshTokenCookieName, out var refreshToken);
        return Task.FromResult(string.IsNullOrEmpty(refreshToken) ? null : refreshToken);
    }

    public Task ClearAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        httpContext.Response.Cookies.Delete(Options.AccessTokenCookieName, BuildAccessCookie(null));
        httpContext.Response.Cookies.Delete(Options.RefreshTokenCookieName, BuildRefreshCookie());

        return Task.CompletedTask;
    }

    /// <summary>
    /// Requires a same-origin fetch and a valid antiforgery token. Without this, a cross-site page
    /// could rotate or revoke a visitor's session simply by causing the request — the browser would
    /// attach the cookies for it.
    /// </summary>
    public async Task<bool> IsRequestIntentionalAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        if (!BlazorBaseCsrfEndpointFilter.IsSameOriginRequest(httpContext))
            return false;

        return await Antiforgery.IsRequestValidAsync(httpContext);
    }

    private CookieOptions BuildAccessCookie(DateTime? expiresAt) => new()
    {
        HttpOnly = true,
        Secure   = Options.SecureOnly,
        SameSite = Options.SameSite,
        Path     = "/",
        Expires  = expiresAt is null ? null : new DateTimeOffset(expiresAt.Value, TimeSpan.Zero),
    };

    private CookieOptions BuildRefreshCookie()
    {
        var days = Options.RefreshCookieDays
                   ?? Configuration.GetValue("JwtSettings:RefreshExpirationDays", JwtSettingsDefaults.RefreshTokenExpirationDays);

        return new CookieOptions
        {
            HttpOnly = true,
            Secure   = Options.SecureOnly,
            SameSite = Options.SameSite,
            Path     = Options.RefreshCookiePath,
            Expires  = DateTimeOffset.UtcNow.AddDays(days),
        };
    }
}
