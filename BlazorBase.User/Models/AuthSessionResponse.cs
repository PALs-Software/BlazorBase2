namespace BlazorBase.User.Models;

/// <summary>
/// The authenticated caller as the server sees them, for a client that cannot inspect its own token.
/// </summary>
/// <remarks>
/// Under the cookie transport the access token is <c>HttpOnly</c> by design, so the client has no way
/// to read the claims it needs for its own UI decisions — which menu entries to show, which route to
/// guard. This is the server telling it. It reveals nothing the caller is not already authenticated
/// as, and it is not an authorization decision: the server re-checks every request regardless of what
/// the client believes.
/// </remarks>
public class AuthSessionResponse
{
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// The antiforgery request token to send back in the configured header on every write. Delivered
    /// in the body rather than a readable cookie: the token is bound to this caller's identity, so
    /// one obtained elsewhere — or planted by a sibling subdomain that can write cookies — does not
    /// validate.
    /// </summary>
    public string CsrfToken { get; set; } = string.Empty;

    public List<AuthSessionClaim> Claims { get; set; } = [];
}

public record AuthSessionClaim(string Type, string Value);
