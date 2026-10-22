using Markdig;
using System.Text.RegularExpressions;
using Markdig.Renderers.Html;
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
/// The overload taking <see cref="TrustedMarkdownOptions"/> relaxes the image rule, and only that and the link
/// behavior, for Markdown that ships with the application.
/// </remarks>
public static partial class MarkdownConverter
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras()
        .UseAutoLinks()
        .DisableHtml()
        .Build();

    private static readonly string[] AllowedSchemes = ["http", "https", "mailto"];

    private static readonly MarkdownPipeline TrustedPipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras()
        .UseAutoLinks()
        .DisableHtml()
        .Build();

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

    /// <summary>
    /// Renders <paramref name="markdown"/> that ships with the application, applying <paramref name="options"/>;
    /// empty for empty input. Raw HTML is still escaped and unsafe URL schemes still become plain text.
    /// </summary>
    public static string ToHtml(string? markdown, TrustedMarkdownOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        var document = Markdown.Parse(markdown, TrustedPipeline);

        foreach (var link in document.Descendants<LinkInline>().ToList())
            ApplyTrusted(link, options);

        foreach (var autolink in document.Descendants<AutolinkInline>().ToList())
            MakeSafe(autolink);

        if (options.AlertTitles is not null)
            foreach (var quote in document.Descendants<QuoteBlock>().ToList())
                ConvertAlert(quote, options.AlertTitles);

        return document.ToHtml(TrustedPipeline);
    }

    private static void ApplyTrusted(LinkInline link, TrustedMarkdownOptions options)
    {
        if (link.IsImage)
        {
            ApplyTrustedImage(link, options);
            return;
        }

        var url = link.Url;

        if (!IsSafeUrl(url))
        {
            ReplaceByText(link);
            return;
        }

        if (IsRelativeUrl(url))
        {
            ApplyRelativeLink(link, options);
            return;
        }

        if (options.OpenExternalLinksInNewTab && IsWebUrl(url))
            OpenInNewTab(link);
    }

    private static void ApplyTrustedImage(LinkInline image, TrustedMarkdownOptions options)
    {
        if (!options.AllowImages)
        {
            MakeSafe(image);
            return;
        }

        var url = image.Url;
        var resolved = IsRelativeUrl(url) && options.ResolveImageUrl is not null ? options.ResolveImageUrl(url!) : url;

        if (string.IsNullOrWhiteSpace(resolved) || !(IsRelativeUrl(resolved) || IsWebUrl(resolved)) || !IsSafeUrl(resolved))
        {
            ReplaceByText(image);
            return;
        }

        image.Url = resolved;
    }

    private static void ApplyRelativeLink(LinkInline link, TrustedMarkdownOptions options)
    {
        if (options.ResolveLinkUrl is null)
            return;

        var target = options.ResolveLinkUrl(link.Url!);

        if (target is null || string.IsNullOrWhiteSpace(target.Url) || !IsSafeUrl(target.Url))
        {
            ReplaceByText(link);
            return;
        }

        link.Url = target.Url;

        if (target.OpenInNewTab)
            OpenInNewTab(link);

        AddDataAttributes(link, target.DataAttributes);
    }

    private static void AddDataAttributes(LinkInline link, IReadOnlyDictionary<string, string>? attributes)
    {
        if (attributes is null)
            return;

        var target = link.GetAttributes();

        foreach (var (name, value) in attributes)
            if (DataAttributeName().IsMatch(name))
                target.AddPropertyIfNotExist(name, value);
    }

    [GeneratedRegex(@"\Adata-[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex DataAttributeName();

    private static void OpenInNewTab(LinkInline link)
    {
        var attributes = link.GetAttributes();
        attributes.AddPropertyIfNotExist("target", "_blank");
        attributes.AddPropertyIfNotExist("rel", "noopener noreferrer");
    }

    private static void ReplaceByText(LinkInline link)
    {
        var text = string.Concat(link.Descendants<LiteralInline>().Select(literal => literal.Content.ToString()));
        link.ReplaceBy(new LiteralInline(text), copyChildren: false);
    }

    private static bool IsWebUrl(string? url) =>
        url is not null && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    private static bool IsRelativeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var trimmed = url.Trim();
        var schemeEnd = trimmed.IndexOf(':');

        if (schemeEnd < 0)
            return true;

        var firstPathCharacter = trimmed.IndexOfAny(['/', '?', '#']);
        return firstPathCharacter >= 0 && firstPathCharacter < schemeEnd;
    }

    private static void ConvertAlert(QuoteBlock quote, IReadOnlyDictionary<string, string> titles)
    {
        if (quote.FirstOrDefault() is not ParagraphBlock { Inline: { } inline } paragraph)
            return;

        var firstLine = new List<Inline>();
        var text = new System.Text.StringBuilder();

        foreach (var child in inline)
        {
            if (child is LineBreakInline)
                break;

            if (child is not LiteralInline literal)
                return;

            firstLine.Add(child);
            text.Append(literal.Content.ToString());
        }

        var match = AlertTag().Match(text.ToString().Trim());

        if (!match.Success)
            return;

        var kind = match.Groups[1].Value.ToUpperInvariant();

        foreach (var part in firstLine)
            part.Remove();

        if (inline.FirstChild is LineBreakInline lineBreak)
            lineBreak.Remove();

        if (inline.FirstChild is null)
            quote.Remove(paragraph);

        var title = titles.TryGetValue(kind, out var configured) ? configured : char.ToUpperInvariant(kind[0]) + kind[1..].ToLowerInvariant();
        var titleInline = new ContainerInline();
        titleInline.AppendChild(new LiteralInline(title));
        var titleBlock = new ParagraphBlock { Inline = titleInline };
        titleBlock.GetAttributes().AddClass("help-callout-title");
        quote.Insert(0, titleBlock);

        var attributes = quote.GetAttributes();
        attributes.AddClass("help-callout");
        attributes.AddClass("help-callout-" + kind.ToLowerInvariant());
        attributes.AddPropertyIfNotExist("role", "note");
    }

    [GeneratedRegex(@"^\[!(NOTE|TIP|IMPORTANT|WARNING|CAUTION)\]$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AlertTag();
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
