using System.Text.RegularExpressions;

namespace BlazorBase.Components.Server.Help;

/// <summary>
/// Removes the parts of a Markdown document that are marked for internal readers only:
/// <c>&lt;!-- Internal-Start --&gt;</c> up to and including the next <c>&lt;!-- Internal-End --&gt;</c>. A start marker
/// without an end marker removes the rest of the document.
/// </summary>
/// <remarks>
/// The match is a superset of the acadon convention (<c>&lt;!-- Internal-Start --&gt;[\w\W\n\r]*?&lt;!-- Internal-End --&gt;</c>
/// of the documentation pipeline): case and whitespace inside the comment do not matter, so no spelling the
/// help panel itself would hide is ever delivered.
/// </remarks>
public static partial class HelpInternalContentFilter
{
    /// <summary>Returns <paramref name="markdown"/> without its internal blocks; the same instance when it has none.</summary>
    public static string Remove(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        return InternalBlock().Replace(markdown, String.Empty);
    }

    [GeneratedRegex(@"<!--\s*Internal-Start\s*-->.*?(?:<!--\s*Internal-End\s*-->|\z)", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InternalBlock();
}
