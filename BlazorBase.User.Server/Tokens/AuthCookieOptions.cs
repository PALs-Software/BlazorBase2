using BlazorBase.User.Server.Services;
using Microsoft.AspNetCore.Http;

namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Cookie name, lifetime and attributes for the Identity application-session cookie set by
/// <see cref="IdentityCookieAuthTransport"/>.
/// </summary>
public class AuthCookieOptions
{
    public const string SectionName = "AuthCookies";

    /// <summary>
    /// Name of the session cookie. Leave unset to fall back to the default described on
    /// <see cref="EffectiveCookieName"/>.
    /// </summary>
    public string? CookieName { get; set; }

    /// <summary>
    /// The cookie name actually applied: <see cref="CookieName"/> when set, otherwise
    /// <c>__Host-bb_session</c> when <see cref="SecureOnly"/> is on or <c>bb_session</c> otherwise.
    /// The <c>__Host-</c> prefix requires <c>Secure</c>, <c>Path=/</c> and no <c>Domain</c>, so it must
    /// not be applied on plain-HTTP local development.
    /// </summary>
    public string EffectiveCookieName => string.IsNullOrEmpty(CookieName)
        ? (SecureOnly ? "__Host-bb_session" : "bb_session")
        : CookieName;

    /// <summary>
    /// Header the client sends the antiforgery request token in. Obtained from
    /// <c>GET /api/auth/session</c>; there is no readable cookie to read it from, because the token is
    /// bound to the caller's identity and must not be plantable by anything that can write cookies.
    /// </summary>
    public string CsrfHeaderName { get; set; } = "X-CSRF-TOKEN";

    /// <summary>
    /// Whether the cookie is marked <c>Secure</c>. Leave <see langword="true"/>; the only reason to
    /// turn it off is plain-HTTP local development, and doing so in a deployed environment sends the
    /// session over the wire in clear.
    /// </summary>
    public bool SecureOnly { get; set; } = true;

    /// <summary>
    /// <c>SameSite</c> mode for the session cookie.
    /// </summary>
    /// <remarks>
    /// <see cref="SameSiteMode.Lax"/> on purpose, and <see cref="SameSiteMode.Strict"/> is the wrong
    /// choice here: an OIDC provider returns the user by a cross-site <c>form_post</c>, and a strict
    /// cookie is not sent on that request — the user would arrive back at the application logged out,
    /// with nothing in the logs to explain it.
    /// </remarks>
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Lax;

    /// <summary>
    /// How long the session survives without activity, refreshed on every request via sliding
    /// expiration — the mechanism the old JWT-in-a-cookie transport had no equivalent for. Defaults to
    /// the same window the old refresh cookie used.
    /// </summary>
    public TimeSpan ExpireTimeSpan { get; set; } = TimeSpan.FromDays(JwtSettingsDefaults.RefreshTokenExpirationDays);
}
