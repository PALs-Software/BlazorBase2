using BlazorBase.User.Server.OAuth.Configuration;
using BlazorBase.User.Server.OAuth.Protocol;

namespace BlazorBase.User.Server.OAuth.Clients;

/// <summary>
/// The rules a redirect URI must satisfy before a client may register it. Registration is the only
/// place a redirect target is judged; afterwards an authorization request must name a registered URI
/// verbatim, which is what keeps the server from becoming an open redirector.
/// </summary>
public static class OAuthRedirectUriValidator
{
    /// <summary>The most redirect URIs one client may register.</summary>
    public const int MaximumRedirectUriCount = 10;

    /// <summary>The longest redirect URI accepted, matching the grant's column length.</summary>
    public const int MaximumRedirectUriLength = 2000;

    /// <summary>
    /// Returns <see langword="null"/> when <paramref name="redirectUri"/> is acceptable under
    /// <paramref name="options"/>, otherwise a description of the rule it breaks.
    /// </summary>
    /// <remarks>
    /// Accepted: an absolute URI without fragment or user information, at most
    /// <see cref="MaximumRedirectUriLength"/> characters, using <c>https</c>, <c>http</c> on a loopback
    /// host, or one of <see cref="OAuthServerOptions.AdditionalRedirectUriSchemes"/>. A non-empty
    /// <see cref="OAuthServerOptions.AllowedRedirectUriHosts"/> additionally restricts the host of every
    /// URI, whatever its scheme.
    /// </remarks>
    public static string? Validate(string? redirectUri, OAuthServerOptions options)
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
            return "A redirect URI must not be empty.";

        if (redirectUri.Length > MaximumRedirectUriLength)
            return $"A redirect URI must not exceed {MaximumRedirectUriLength} characters.";

        if (redirectUri.Contains('#'))
            return "A redirect URI must not contain a fragment.";

        if (redirectUri != redirectUri.Trim() || redirectUri.Any(char.IsControl))
            return "A redirect URI must not contain whitespace padding or control characters.";

        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var parsedUri))
            return $"'{redirectUri}' is not an absolute URI.";

        if (!string.IsNullOrEmpty(parsedUri.UserInfo))
            return "A redirect URI must not contain user information.";

        if (!IsAcceptedScheme(parsedUri, options))
            return $"The redirect URI scheme '{parsedUri.Scheme}' is not allowed: use https, http on a loopback host, or a configured private-use scheme.";

        if (options.AllowedRedirectUriHosts.Count > 0 && !options.AllowedRedirectUriHosts.Contains(parsedUri.Host, StringComparer.OrdinalIgnoreCase))
            return $"The redirect URI host '{parsedUri.Host}' is not allowed by this server.";

        return null;
    }

    private static bool IsAcceptedScheme(Uri redirectUri, OAuthServerOptions options)
    {
        if (redirectUri.Scheme == Uri.UriSchemeHttps)
            return true;

        if (redirectUri.Scheme == Uri.UriSchemeHttp)
            return OAuthLoopbackHosts.IsLoopback(redirectUri.Host);

        return options.AdditionalRedirectUriSchemes.Contains(redirectUri.Scheme, StringComparer.OrdinalIgnoreCase);
    }
}
