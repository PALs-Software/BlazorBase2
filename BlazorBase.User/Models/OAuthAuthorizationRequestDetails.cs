namespace BlazorBase.User.Models;

/// <summary>
/// What the consent page shows about a pending OAuth authorization request
/// (<c>GET api/oauth/authorization-requests</c>). <see cref="ClientName"/> is asserted by the client
/// itself; <see cref="RedirectHost"/> is where the browser will actually be sent, which is why the page
/// shows both.
/// </summary>
public class OAuthAuthorizationRequestDetails
{
    public string ClientName { get; set; } = string.Empty;

    public string RedirectHost { get; set; } = string.Empty;

    /// <summary>
    /// The host that published the client's metadata document, for a client identified by a Client ID
    /// Metadata Document URL; <see langword="null"/> for a registered client. Like
    /// <see cref="RedirectHost"/> it cannot be chosen freely by whoever starts the request.
    /// </summary>
    public string? ClientIdHost { get; set; }

    public List<OAuthScopeDetails> Scopes { get; set; } = [];
}
