namespace BlazorBase.User.Server.OAuth.Authorization;

/// <summary>
/// A validated authorization request, carried from <c>authorize</c> through the consent page to the
/// decision endpoint inside a time-limited data-protected string - so the server keeps no state for a
/// request the user never answers, and nothing in it can be altered on the way.
/// </summary>
/// <param name="ClientId">The requesting client.</param>
/// <param name="RedirectUri">The registered redirect URI the response goes to.</param>
/// <param name="RedirectUriSupplied">
/// Whether the request named <paramref name="RedirectUri"/> itself (rather than relying on the client's
/// single registered URI); a token request must then repeat it.
/// </param>
/// <param name="CodeChallenge">The PKCE <c>S256</c> challenge.</param>
/// <param name="State">The client's opaque <c>state</c>, echoed back unchanged.</param>
/// <param name="Scope">The space-separated scopes requested, already checked against the configured ones.</param>
/// <param name="Resource">The RFC 8707 resource indicator, already checked against the registered resources.</param>
public sealed record OAuthAuthorizationRequest(
    string ClientId,
    string RedirectUri,
    bool RedirectUriSupplied,
    string CodeChallenge,
    string? State,
    string Scope,
    string? Resource);
