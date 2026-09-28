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
    public TimeSpan ExpireTimeSpan { get; set; } = TimeSpan.FromDays(AuthTokenDefaults.RefreshTokenExpirationDays);

    /// <summary>
    /// Whether a request whose <c>Sec-Fetch-Site</c> header reads <c>same-site</c> — a different
    /// subdomain of the same registrable domain, such as <c>app.example.com</c> calling
    /// <c>api.example.com</c> — is treated as same-origin for CSRF purposes.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="false"/>: the cookie transport's default posture requires the
    /// caller to be the exact origin the cookie was issued to, and a hostile or merely compromised
    /// sibling subdomain is "same-site" too, so accepting it widens who can ride the session cookie.
    /// A host that deliberately splits its browser app and API across subdomains of one registrable
    /// domain — the <c>app.</c> / <c>api.</c> split documented for the WASM client — sets this to
    /// <see langword="true"/> to make that deployment work; doing so relies on the antiforgery token
    /// check alone to stop a cross-site request, since <c>Sec-Fetch-Site</c> no longer distinguishes
    /// the two. Only turn this on when the split-origin deployment is the one actually in use.
    /// </remarks>
    public bool AllowSameSiteRequests { get; set; }

    /// <summary>
    /// Whether sign-out revokes the session server-side through <see cref="BlazorBaseTicketStore"/>,
    /// so a cookie value captured before logout no longer authenticates if replayed afterward.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="true"/>: the framework's own default must be the one where logout
    /// means logout. Turn this off only when the host already has its own distributed
    /// <c>ITicketStore</c> — or another server-side revocation mechanism entirely — and registers it
    /// through <c>CookieAuthenticationOptions.SessionStore</c> directly; leaving this on as well would
    /// only mean an unused in-memory store sits beside the one actually in effect.
    /// </remarks>
    public bool ServerSideSessionStoreEnabled { get; set; } = true;
}
