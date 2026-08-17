namespace BlazorBase.CRUD.Navigation;

/// <summary>
/// Reads a single query-string parameter value from an absolute or relative URI. Kept dependency-free (no
/// <c>Microsoft.AspNetCore.WebUtilities</c>) and pure so it can be unit-tested in isolation. Used by
/// <see cref="Components.BaseList{TModel}"/> to resolve the deep-linked item from the current URL.
/// </summary>
public static class QueryStringReader
{
    /// <summary>
    /// Returns the decoded value of <paramref name="parameterName"/> from the query part of <paramref name="uri"/>,
    /// or <c>null</c> when the parameter is absent. A present parameter without a value yields an empty string.
    /// The first occurrence wins and the key match is case-insensitive.
    /// </summary>
    public static string? TryReadValue(string uri, string parameterName)
    {
        if (string.IsNullOrEmpty(uri) || string.IsNullOrEmpty(parameterName))
            return null;

        var queryStart = uri.IndexOf('?');

        if (queryStart < 0 || queryStart == uri.Length - 1)
            return null;

        var query = uri[(queryStart + 1)..];

        var fragmentStart = query.IndexOf('#');

        if (fragmentStart >= 0)
            query = query[..fragmentStart];

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = separator < 0 ? pair : pair[..separator];

            if (!string.Equals(Uri.UnescapeDataString(key), parameterName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (separator < 0 || separator == pair.Length - 1)
                return string.Empty;

            return Uri.UnescapeDataString(pair[(separator + 1)..]);
        }

        return null;
    }
}
