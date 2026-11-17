namespace BlazorBase.User.Server.OAuth.Protocol;

/// <summary>
/// Appends authorization-response parameters to a registered redirect URI, keeping whatever query the
/// client registered (RFC 6749 section 3.1.2 requires it to be retained).
/// </summary>
public static class OAuthRedirectUriComposer
{
    /// <summary>
    /// Returns <paramref name="redirectUri"/> with every parameter whose value is not
    /// <see langword="null"/> appended to its query, URL-encoded, after the query it already has.
    /// </summary>
    public static string AppendParameters(string redirectUri, params (string Name, string? Value)[] parameters)
    {
        var encodedParameters = parameters
            .Where(parameter => parameter.Value is not null)
            .Select(parameter => $"{Uri.EscapeDataString(parameter.Name)}={Uri.EscapeDataString(parameter.Value!)}")
            .ToList();

        if (encodedParameters.Count == 0)
            return redirectUri;

        var separator = redirectUri.Contains('?')
            ? redirectUri.EndsWith('?') || redirectUri.EndsWith('&') ? string.Empty : "&"
            : "?";

        return redirectUri + separator + string.Join('&', encodedParameters);
    }
}
