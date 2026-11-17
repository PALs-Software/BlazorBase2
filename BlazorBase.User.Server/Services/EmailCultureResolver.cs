using System.Globalization;

namespace BlazorBase.User.Server.Services;

/// <summary>Turns a stored language code into the culture an account email is rendered in.</summary>
public static class EmailCultureResolver
{
    /// <summary>
    /// Returns the culture for <paramref name="language"/>, or <see langword="null"/> (the ambient culture)
    /// when it is empty or not a known culture name.
    /// </summary>
    public static CultureInfo? Resolve(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;

        try
        {
            return CultureInfo.GetCultureInfo(language.Trim());
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
