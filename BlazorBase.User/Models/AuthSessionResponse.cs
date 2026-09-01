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

    public List<AuthSessionClaim> Claims { get; set; } = [];
}

public record AuthSessionClaim(string Type, string Value);
