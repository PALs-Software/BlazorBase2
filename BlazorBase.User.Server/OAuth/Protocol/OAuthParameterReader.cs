using Microsoft.Extensions.Primitives;

namespace BlazorBase.User.Server.OAuth.Protocol;

/// <summary>
/// Reads one OAuth request parameter from a query or form collection. RFC 6749 section 3.1 forbids
/// repeating a parameter, so a repeated one is reported rather than silently resolved to either copy.
/// </summary>
public static class OAuthParameterReader
{
    /// <summary>
    /// Returns <see langword="false"/> when <paramref name="values"/> holds more than one value;
    /// otherwise sets <paramref name="value"/> to the single value, or <see langword="null"/> when the
    /// parameter is absent or empty.
    /// </summary>
    public static bool TryReadSingle(StringValues values, out string? value)
    {
        value = null;

        if (values.Count > 1)
            return false;

        var single = values.Count == 1 ? values[0] : null;
        value = string.IsNullOrEmpty(single) ? null : single;
        return true;
    }
}
