using System.Globalization;

namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// Resolves the key under which a list's saved filters are stored. The default is
/// <c>&lt;model full name&gt;@&lt;route&gt;</c>, so two pages that list the same model do not share filters or defaults.
/// An explicit key always wins unchanged. Pure and WASM-safe: no server-only APIs.
/// </summary>
public static class SavedFilterKey
{
    private const char Separator = '@';

    private const char Shortened = '~';

    private const int HashLength = 16;

    private const int MaxModelPartLength = 160;

    /// <summary>
    /// Returns the explicit key when one is given, otherwise <c>&lt;model full name&gt;@&lt;route&gt;</c> with a route
    /// from <see cref="NormalizeRoute"/>. The result never exceeds <see cref="ListFilterValidator.MaxListKeyLength"/>.
    /// </summary>
    /// <param name="explicitKey">A key chosen by the developer; returned as is.</param>
    /// <param name="modelType">The listed model.</param>
    /// <param name="currentUri">The absolute URI of the page, as <c>NavigationManager.Uri</c> reports it.</param>
    /// <param name="baseUri">The base URI, as <c>NavigationManager.BaseUri</c> reports it (ends with the PWA base href).</param>
    public static string Resolve(string? explicitKey, Type modelType, string? currentUri, string? baseUri)
    {
        if (explicitKey is not null)
            return explicitKey;

        var model = Shorten(modelType.FullName ?? modelType.Name, MaxModelPartLength);
        var route = NormalizeRoute(currentUri, baseUri);
        var routeBudget = ListFilterValidator.MaxListKeyLength - model.Length - 1;

        return $"{model}{Separator}{Shorten(route, routeBudget)}";
    }

    /// <summary>
    /// The page path relative to the base address, without query string and fragment, lower-cased and trimmed of
    /// slashes, with a leading slash; the root is <c>/</c>.
    /// </summary>
    public static string NormalizeRoute(string? currentUri, string? baseUri)
    {
        var path = ExtractPath(currentUri);
        var basePath = ExtractPath(baseUri).TrimEnd('/');

        if (basePath.Length > 0 && path.StartsWith(basePath, StringComparison.OrdinalIgnoreCase)
            && (path.Length == basePath.Length || path[basePath.Length] == '/'))
            path = path[basePath.Length..];

        var trimmed = path.Trim('/').ToLower(CultureInfo.InvariantCulture);
        return $"/{trimmed}";
    }

    private static string ExtractPath(string? uri)
    {
        if (String.IsNullOrWhiteSpace(uri))
            return "/";

        if (Uri.TryCreate(uri, UriKind.Absolute, out var absolute))
            return absolute.AbsolutePath;

        var end = uri.IndexOfAny(['?', '#']);
        return end < 0 ? uri : uri[..end];
    }

    private static string Shorten(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        var hash = Hash(value);
        var keep = Math.Max(0, maxLength - HashLength - 1);
        return $"{value[..keep]}{Shortened}{hash}";
    }

    private static string Hash(string value)
    {
        var hash = 14695981039346656037UL;

        foreach (var character in value)
        {
            hash = unchecked((hash ^ character) * 1099511628211UL);
        }

        return hash.ToString("x16", CultureInfo.InvariantCulture);
    }
}
