namespace BlazorBase.User.Server.OAuth.Protocol;

/// <summary>
/// Compares RFC 8707 resource indicators the way clients actually send them: absolute URIs whose
/// scheme and host are case-insensitive and whose trailing slash carries no meaning.
/// </summary>
public static class OAuthResourceIdentifier
{
    /// <summary>
    /// Returns the comparable form of <paramref name="resource"/> (lower-case scheme and host, default
    /// port omitted, no trailing slash), or <see langword="null"/> when it is not an absolute URI or
    /// carries a fragment, which RFC 8707 forbids.
    /// </summary>
    public static string? Normalize(string? resource)
    {
        if (string.IsNullOrWhiteSpace(resource) || resource.Contains('#'))
            return null;

        if (!Uri.TryCreate(resource, UriKind.Absolute, out var resourceUri))
            return null;

        var authority = resourceUri.IsDefaultPort ? resourceUri.Host : $"{resourceUri.Host}:{resourceUri.Port}";
        var path = resourceUri.AbsolutePath.TrimEnd('/');

        return $"{resourceUri.Scheme.ToLowerInvariant()}://{authority.ToLowerInvariant()}{path}{resourceUri.Query}";
    }

    /// <summary>Returns <see langword="true"/> when both values normalize to the same resource.</summary>
    public static bool AreEquivalent(string? first, string? second)
    {
        var normalizedFirst = Normalize(first);
        return normalizedFirst is not null && string.Equals(normalizedFirst, Normalize(second), StringComparison.Ordinal);
    }
}
