namespace BlazorBase.User.Server.OAuth.Protocol;

/// <summary>Parsing and validation of space-delimited OAuth scope strings (RFC 6749 section 3.3).</summary>
public static class OAuthScopes
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="scope"/> consists only of the characters
    /// RFC 6749 allows in a scope token: printable ASCII except space, double quote and backslash.
    /// </summary>
    public static bool IsValidScopeToken(string scope)
    {
        if (string.IsNullOrEmpty(scope))
            return false;

        return scope.All(character => character == '!' || (character >= '#' && character <= '[') || (character >= ']' && character <= '~'));
    }

    /// <summary>Splits a space-delimited scope string into its distinct tokens, keeping their order.</summary>
    public static IReadOnlyList<string> Parse(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
            return [];

        return scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>Joins scope tokens into the space-delimited wire form.</summary>
    public static string Join(IEnumerable<string> scopes) => string.Join(' ', scopes);
}
