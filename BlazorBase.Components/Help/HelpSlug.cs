using System.Globalization;
using System.Text;

namespace BlazorBase.Components.Help;

/// <summary>Turns heading and field texts into stable, URL- and id-safe slugs.</summary>
/// <remarks>
/// Letters are lower-cased, German umlauts and <c>ß</c> become <c>ae</c>, <c>oe</c>, <c>ue</c> and <c>ss</c>, other
/// accents are dropped, every run of other characters becomes one hyphen and leading and trailing hyphens are removed.
/// </remarks>
public static class HelpSlug
{
    /// <summary>The slug of a text that has no letter or digit.</summary>
    public const string Fallback = "section";

    /// <summary>Returns the slug of <paramref name="text"/>, or <see cref="Fallback"/> when nothing is left of it.</summary>
    public static string Create(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Fallback;

        var decomposed = Transliterate(text.Trim().ToLowerInvariant()).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingHyphen = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (!char.IsLetterOrDigit(character))
            {
                pendingHyphen = builder.Length > 0;
                continue;
            }

            if (pendingHyphen)
                builder.Append('-');

            pendingHyphen = false;
            builder.Append(character);
        }

        return builder.Length == 0 ? Fallback : builder.ToString();
    }

    /// <summary>
    /// Returns <paramref name="prefix"/> plus the slug of <paramref name="text"/>, with <c>-2</c>, <c>-3</c>, … appended
    /// while the result is already in <paramref name="used"/>, and adds the result to <paramref name="used"/>.
    /// </summary>
    public static string CreateUnique(string? text, ISet<string> used, string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(used);

        var baseId = prefix + Create(text);
        var id = baseId;

        for (var number = 2; !used.Add(id); number++)
            id = baseId + "-" + number.ToString(CultureInfo.InvariantCulture);

        return id;
    }

    private static string Transliterate(string text) => text
        .Replace("ä", "ae", StringComparison.Ordinal)
        .Replace("ö", "oe", StringComparison.Ordinal)
        .Replace("ü", "ue", StringComparison.Ordinal)
        .Replace("ß", "ss", StringComparison.Ordinal);
}
