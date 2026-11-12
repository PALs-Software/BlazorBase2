using Microsoft.AspNetCore.Http;

namespace BlazorBase.User.Server.OAuth.Metadata;

/// <summary>
/// The paths registered through <c>MapBlazorBaseOAuthProtectedResource</c>: each has an RFC 9728
/// metadata document, may be named as a <c>resource</c> indicator, and gets <c>resource_metadata</c> in
/// its 401 challenge. The first registered path also answers the bare
/// <c>/.well-known/oauth-protected-resource</c>.
/// </summary>
public sealed class OAuthProtectedResourceRegistry
{
    private readonly Lock SyncRoot = new();
    private readonly List<string> Paths = [];

    /// <summary>The registered resource paths in registration order, each with a leading and no trailing slash.</summary>
    public IReadOnlyList<string> ResourcePaths
    {
        get
        {
            lock (SyncRoot)
                return Paths.ToList();
        }
    }

    /// <summary>Registers <paramref name="resourcePath"/> once and returns its normalized form.</summary>
    public string Register(string resourcePath)
    {
        var normalizedPath = Normalize(resourcePath);

        lock (SyncRoot)
        {
            if (!Paths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
                Paths.Add(normalizedPath);
        }

        return normalizedPath;
    }

    /// <summary>
    /// Returns the registered path that <paramref name="requestPath"/> equals or lies below (matched on
    /// whole segments, so <c>/api/mcp</c> covers <c>/api/mcp/sse</c> but not <c>/api/mcpx</c>), or
    /// <see langword="null"/>.
    /// </summary>
    public string? FindMatchingResourcePath(PathString requestPath) =>
        ResourcePaths.FirstOrDefault(resourcePath => requestPath.StartsWithSegments(resourcePath, StringComparison.OrdinalIgnoreCase));

    /// <summary>Brings <paramref name="resourcePath"/> to the form the registry stores: one leading slash, no trailing slash.</summary>
    public static string Normalize(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath) || resourcePath.Trim('/').Length == 0)
            throw new ArgumentException("A protected resource path must name a path below the application root, for example \"/api/mcp\".", nameof(resourcePath));

        if (resourcePath.Contains('?') || resourcePath.Contains('#'))
            throw new ArgumentException("A protected resource path must not carry a query or fragment.", nameof(resourcePath));

        return "/" + resourcePath.Trim().Trim('/');
    }
}
