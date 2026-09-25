using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace BlazorBase.Components.Html;

/// <summary>
/// Converts Markdown to HTML that is safe to render even without an <c>IHtmlSanitizer</c>, for text written
/// by someone else - a language model's answer, a user's comment.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Raw HTML in the Markdown is escaped, never passed through.</item>
/// <item>Only <c>http</c>, <c>https</c>, <c>mailto</c> and relative link targets survive; any other link
/// (<c>javascript:</c>, <c>data:</c>, obfuscated with control characters or entities) becomes plain text.</item>
/// <item>Images become links carrying their alt text, so rendering never makes the browser contact a
/// server the text names.</item>
/// <item>The pipeline enables pipe tables, strikethrough and bare-URL links only. Markdig's
/// <c>UseAdvancedExtensions()</c> is deliberately avoided: its generic-attributes extension turns
/// <c>{onclick=…}</c> into real HTML attributes.</item>
/// </list>
/// </remarks>
public static class MarkdownConverter
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras()
        .UseAutoLinks()
        .DisableHtml()
        .Build();

    private static readonly string[] AllowedSchemes = ["http", "https", "mailto"];

    /// <summary>Renders <paramref name="markdown"/> as safe HTML; empty for empty input.</summary>
    public static string ToHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        var document = Markdown.Parse(markdown, Pipeline);

        foreach (var link in document.Descendants<LinkInline>().ToList())
            MakeSafe(link);

        foreach (var autolink in document.Descendants<AutolinkInline>().ToList())
            MakeSafe(autolink);

        return document.ToHtml(Pipeline);
    }

    private static void MakeSafe(LinkInline link)
    {
        link.IsImage = false;

        if (IsSafeUrl(link.Url))
            return;

        var text = string.Concat(link.Descendants<LiteralInline>().Select(literal => literal.Content.ToString()));
        link.ReplaceBy(new LiteralInline(text), copyChildren: false);
    }

    private static void MakeSafe(AutolinkInline autolink)
    {
        if (IsSafeUrl(autolink.Url))
            return;

        autolink.ReplaceBy(new LiteralInline(autolink.Url), copyChildren: false);
    }

    private static bool IsSafeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var normalized = new string(url.Where(character => !char.IsControl(character) && !char.IsWhiteSpace(character)).ToArray())
            .ToLowerInvariant();

        var schemeEnd = normalized.IndexOf(':');

        if (schemeEnd < 0)
            return true;

        var firstPathCharacter = normalized.IndexOfAny(['/', '?', '#']);

        if (firstPathCharacter >= 0 && firstPathCharacter < schemeEnd)
            return true;

        return AllowedSchemes.Contains(normalized[..schemeEnd]);
    }
}
