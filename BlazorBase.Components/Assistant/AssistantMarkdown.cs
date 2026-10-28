using BlazorBase.Components.Html;
using Markdig;
using Markdig.Syntax;

namespace BlazorBase.Components.Assistant;

/// <summary>A part of an answer: rendered HTML, or a code block that gets a copy button.</summary>
/// <param name="Html">The safe HTML of a text part; empty for a code part.</param>
/// <param name="Code">The code of a code part; null for a text part.</param>
/// <param name="Language">The language named after the fence, when there is one.</param>
internal sealed record AssistantMarkdownSegment(string Html, string? Code = null, string? Language = null)
{
    public bool IsCode => Code is not null;
}

/// <summary>
/// Turns the Markdown of an answer into parts the panel renders. Text is converted by
/// <see cref="MarkdownConverter"/> — raw HTML is escaped, only <c>http</c>, <c>https</c>, <c>mailto</c> and relative
/// links survive, images become links — and external links open in a new tab with <c>rel="noopener noreferrer"</c>.
/// Fenced code blocks become separate parts and are rendered by the panel as encoded text.
/// </summary>
internal static class AssistantMarkdown
{
    private static readonly MarkdownPipeline BlockPipeline = new MarkdownPipelineBuilder().UsePipeTables().DisableHtml().Build();

    private static readonly TrustedMarkdownOptions TextOptions = new()
    {
        OpenExternalLinksInNewTab = true,
        ResolveLinkUrl = ResolveRelativeLink
    };

    public static IReadOnlyList<AssistantMarkdownSegment> Split(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return [];

        var document = Markdown.Parse(markdown, BlockPipeline);
        var segments = new List<AssistantMarkdownSegment>();
        var position = 0;

        foreach (var block in document.OfType<FencedCodeBlock>())
        {
            AddText(segments, markdown, position, block.Span.Start);
            segments.Add(new AssistantMarkdownSegment(string.Empty, block.Lines.ToString().TrimEnd('\r', '\n'), NormalizeLanguage(block.Info)));
            position = block.ClosingFencedCharCount == 0 ? markdown.Length : Math.Min(markdown.Length, block.Span.End + 1);
        }

        AddText(segments, markdown, position, markdown.Length);
        return segments;
    }

    private static void AddText(List<AssistantMarkdownSegment> segments, string markdown, int start, int end)
    {
        if (end <= start)
            return;

        var html = MarkdownConverter.ToHtml(markdown[start..end], TextOptions);

        if (html.Length > 0)
            segments.Add(new AssistantMarkdownSegment(html));
    }

    private static string? NormalizeLanguage(string? info)
    {
        var language = info?.Split([' ', '\t', '{'], 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(language) ? null : language;
    }

    private static MarkdownLink? ResolveRelativeLink(string url) =>
        url.StartsWith("//", StringComparison.Ordinal) || url.Contains('\\') ? null : new MarkdownLink(url);
}
