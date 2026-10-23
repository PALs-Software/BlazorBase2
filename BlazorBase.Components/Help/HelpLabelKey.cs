using System.Text.RegularExpressions;

namespace BlazorBase.Components.Help;

/// <summary>
/// Normalizes the visible text of a label so the text on the page and the label of a help entry can be compared:
/// whitespace collapsed and trimmed, trailing <c>:</c> and <c>*</c> removed, lower-cased. The script that finds the
/// labels on the page applies the same rules.
/// </summary>
public static partial class HelpLabelKey
{
    /// <summary>Returns the normalized key of <paramref name="text"/>; empty when nothing is left.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var collapsed = Whitespace().Replace(text, " ").Trim();
        return collapsed.TrimEnd(':', '*', ' ').ToLowerInvariant();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
