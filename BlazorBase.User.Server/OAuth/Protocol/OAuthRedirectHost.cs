namespace BlazorBase.User.Server.OAuth.Protocol;

/// <summary>
/// Turns a redirect URI into the short form shown to the user - the host the browser is actually sent
/// to, which unlike the self-asserted client name cannot be chosen freely by whoever registered the client.
/// </summary>
public static class OAuthRedirectHost
{
    /// <summary>
    /// Returns the authority (host, plus the port when it is not the scheme's default) of
    /// <paramref name="redirectUri"/>, <c>scheme:</c> for a private-use URI without one, or the input
    /// unchanged when it is not an absolute URI.
    /// </summary>
    public static string Describe(string redirectUri)
    {
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var parsedUri))
            return redirectUri;

        if (!string.IsNullOrEmpty(parsedUri.Authority))
            return parsedUri.Authority;

        return parsedUri.Scheme + ":";
    }
}
