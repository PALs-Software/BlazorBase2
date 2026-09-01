using Microsoft.AspNetCore.Http;

namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Cookie names, lifetimes and attributes used by <see cref="CookieAuthTokenTransport"/>.
/// </summary>
public class AuthCookieOptions
{
    public const string SectionName = "AuthCookies";

    /// <summary>Cookie carrying the access token. Read by the JWT handler when no bearer header is present.</summary>
    public string AccessTokenCookieName { get; set; } = "bb_access";

    /// <summary>Cookie carrying the refresh token.</summary>
    public string RefreshTokenCookieName { get; set; } = "bb_refresh";

    /// <summary>
    /// Header the client sends the antiforgery request token in. Obtained from
    /// <c>GET /api/auth/session</c>; there is no readable cookie to read it from, because the token is
    /// bound to the caller's identity and must not be plantable by anything that can write cookies.
    /// </summary>
    public string CsrfHeaderName { get; set; } = "X-CSRF-TOKEN";

    /// <summary>
    /// Path the refresh cookie is scoped to, so it is not attached to every API call. Must match the
    /// route the refresh endpoint is actually mapped at.
    /// </summary>
    public string RefreshCookiePath { get; set; } = "/api/auth";

    /// <summary>
    /// Whether cookies are marked <c>Secure</c>. Leave <see langword="true"/>; the only reason to turn
    /// it off is plain-HTTP local development, and doing so in a deployed environment sends the
    /// session over the wire in clear.
    /// </summary>
    public bool SecureOnly { get; set; } = true;

    /// <summary>
    /// <c>SameSite</c> mode for the session cookies.
    /// </summary>
    /// <remarks>
    /// <see cref="SameSiteMode.Lax"/> on purpose, and <see cref="SameSiteMode.Strict"/> is the wrong
    /// choice here: an OIDC provider returns the user by a cross-site <c>form_post</c>, and a strict
    /// cookie is not sent on that request — the user would arrive back at the application logged out,
    /// with nothing in the logs to explain it.
    /// </remarks>
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Lax;

    /// <summary>
    /// How long the refresh cookie survives in the browser. Defaults to the configured refresh-token
    /// lifetime; the server-side token remains the authority either way, so a stale cookie only ever
    /// results in a rejected rotation, never in extended access.
    /// </summary>
    public int? RefreshCookieDays { get; set; }
}
