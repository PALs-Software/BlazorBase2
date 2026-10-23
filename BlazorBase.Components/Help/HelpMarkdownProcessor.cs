using System.Text;
using System.Text.RegularExpressions;
using BlazorBase.Components.Html;

namespace BlazorBase.Components.Help;

/// <summary>
/// Turns a DocFX Markdown page into what the help panel shows: removes the front matter and the parts meant for
/// the documentation site only, resolves includes, and describes how links and images are rewritten.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><c>&lt;!-- Docs-Only-Start --&gt;</c> … <c>&lt;!-- Docs-Only-End --&gt;</c> and <c>&lt;!-- Internal-Start --&gt;</c> … <c>&lt;!-- Internal-End --&gt;</c>
/// are removed, inline or as a block; a start marker without an end marker removes the rest of the document.</item>
/// <item><c>&lt;!-- App-Only … --&gt;</c> is unwrapped: its Markdown is shown, while DocFX sees a comment.</item>
/// <item><c>&lt;!-- help:fields --&gt;</c> marks the start of the field entries: it is removed from the Markdown and its
/// line is kept in <see cref="HelpDocument.FieldsStartLine"/> (see <see cref="HelpStructureReader"/>).</item>
/// <item>Other HTML comments are removed.</item>
/// <item><c>[!INCLUDE [label](path)]</c> is replaced by the file, relative to the including file, up to three
/// levels deep; a file that includes itself, directly or through others, is skipped.</item>
/// <item>The first heading, when the document starts with one, becomes <see cref="HelpDocument.Title"/> unless the
/// front matter has a <c>title</c>, and is left out of the body because the panel shows the title itself.</item>
/// </list>
/// Relative links and images, including those of included files, are resolved against the including document,
/// as DocFX does. A link to a <c>.md</c> file becomes a link to the page that carries the topic, or — when no page
/// does, e.g. for a category topic — an in-panel link (<see cref="TopicLinkAttribute"/>) that the panel opens itself;
/// other relative files link to the documentation site. <see cref="CreateRenderOptions(HelpProcessingContext)"/> describes the rewriting; see <see cref="TrustedMarkdownOptions"/>.
/// </remarks>
public static partial class HelpMarkdownProcessor
{
    private const int MaxIncludeDepth = 3;

    /// <summary>The attribute that marks a rendered link as pointing at a help topic that opens inside the panel; its value is the topic.</summary>
    public const string TopicLinkAttribute = "data-help-topic";

    /// <summary>The <c>href</c> prefix of such a link, followed by the topic; a real anchor so the link stays focusable and keyboard operable.</summary>
    public const string TopicLinkPrefix = "#help:";

    private static readonly IReadOnlyDictionary<string, string> DefaultAlertTitles = new Dictionary<string, string>
    {
        ["NOTE"] = "Note",
        ["TIP"] = "Tip",
        ["IMPORTANT"] = "Important",
        ["WARNING"] = "Warning",
        ["CAUTION"] = "Caution",
    };

    /// <summary>Prepares <paramref name="markdown"/>, the raw content of the document described by <paramref name="context"/>.</summary>
    public static async Task<HelpDocument> ProcessAsync(string markdown, HelpProcessingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (frontMatterTitle, body) = SplitFrontMatter(Normalize(markdown));
        var cleaned = RemoveComments(ApplyMarkers(body));
        var path = context.Topic + ".md";
        var expanded = await ExpandIncludesAsync(cleaned, HelpPaths.DirectoryOf(path), context, [path], 0, cancellationToken);
        var (headingTitle, withoutHeading) = ExtractTitle(expanded);
        var (finalMarkdown, fieldsStartLine) = ExtractFieldsMarker(withoutHeading.Trim());

        return new HelpDocument(frontMatterTitle ?? headingTitle, finalMarkdown, fieldsStartLine);
    }

    private static (string Markdown, int? FieldsStartLine) ExtractFieldsMarker(string markdown)
    {
        var first = FieldsMarker().Match(markdown);

        if (!first.Success)
            return (markdown, null);

        var line = markdown.AsSpan(0, first.Index).Count('\n');

        return (FieldsMarker().Replace(markdown, string.Empty), line);
    }

    /// <summary>Describes how the Markdown of the document is rendered: images from the help folder, links to topics to their routes.</summary>
    public static TrustedMarkdownOptions CreateRenderOptions(HelpProcessingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new TrustedMarkdownOptions
        {
            AllowImages = true,
            OpenExternalLinksInNewTab = true,
            ResolveImageUrl = url => ResolveImage(url, context),
            ResolveLinkUrl = url => ResolveLink(url, context),
            AlertTitles = context.AlertTitles ?? DefaultAlertTitles,
        };
    }

    /// <summary>
    /// Describes how <paramref name="document"/> is rendered: as <see cref="CreateRenderOptions(HelpProcessingContext)"/>, plus
    /// the ids of its headings and field entries (see <see cref="HelpStructureReader"/>), so the panel can scroll to them.
    /// </summary>
    public static TrustedMarkdownOptions CreateRenderOptions(HelpProcessingContext context, HelpDocument document)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(document);

        var fieldsStartLine = document.FieldsStartLine;
        var options = CreateRenderOptions(context);

        return new TrustedMarkdownOptions
        {
            AllowImages = options.AllowImages,
            OpenExternalLinksInNewTab = options.OpenExternalLinksInNewTab,
            ResolveImageUrl = options.ResolveImageUrl,
            ResolveLinkUrl = options.ResolveLinkUrl,
            AlertTitles = options.AlertTitles,
            PrepareDocument = parsed => HelpStructureReader.Analyze(parsed, fieldsStartLine, applyIds: true),
        };
    }

    /// <summary>Renders <paramref name="document"/> as HTML with the options of <see cref="CreateRenderOptions(HelpProcessingContext, HelpDocument)"/>.</summary>
    public static string ToHtml(HelpDocument document, HelpProcessingContext context)
    {
        ArgumentNullException.ThrowIfNull(document);

        return MarkdownConverter.ToHtml(document.Markdown, CreateRenderOptions(context, document));
    }

    /// <summary>Returns the address of <paramref name="topic"/> on the documentation site, or null when no site is configured.</summary>
    public static string? GetDocumentationUrl(string topic, string? documentationBaseUrl, string culture) =>
        CombineDocumentationUrl(documentationBaseUrl, culture, topic + ".html");

    private static string Normalize(string markdown) => markdown.Replace("\r\n", "\n").Replace('\r', '\n');

    private static (string? Title, string Body) SplitFrontMatter(string markdown)
    {
        var match = FrontMatter().Match(markdown);

        if (!match.Success)
            return (null, markdown);

        var title = FrontMatterTitle().Match(match.Groups[1].Value).Groups[1].Value.Trim().Trim('"', '\'').Trim();

        return (title.Length == 0 ? null : title, markdown[match.Length..]);
    }

    private static string ApplyMarkers(string markdown)
    {
        var withoutHidden = HiddenBlock().Replace(markdown, string.Empty);

        return AppOnly().Replace(withoutHidden, match =>
        {
            var content = match.Groups[1].Value.Trim();
            return content.Contains('\n') ? "\n\n" + content + "\n\n" : content;
        });
    }

    private static string RemoveComments(string markdown) => Comment().Replace(markdown, string.Empty);

    private static (string? Title, string Body) ExtractTitle(string markdown)
    {
        var lines = markdown.Split('\n');
        var index = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line));

        if (index < 0)
            return (null, markdown);

        var match = Heading().Match(lines[index]);

        if (!match.Success)
            return (null, markdown);

        return (match.Groups[1].Value.Trim(), string.Join('\n', lines.Take(index).Concat(lines.Skip(index + 1))));
    }

    private static async Task<string> ExpandIncludesAsync(
        string markdown,
        string directory,
        HelpProcessingContext context,
        IReadOnlyList<string> stack,
        int depth,
        CancellationToken cancellationToken)
    {
        var matches = IncludeTag().Matches(markdown);

        if (matches.Count == 0)
            return markdown;

        var builder = new StringBuilder();
        var position = 0;

        foreach (Match match in matches)
        {
            var content = await LoadIncludeAsync(match.Groups[1].Value, directory, context, stack, depth, cancellationToken);
            var lineStart = match.Index == 0 ? 0 : markdown.LastIndexOf('\n', match.Index - 1) + 1;
            var matchEnd = match.Index + match.Length;
            var lineEnd = markdown.IndexOf('\n', matchEnd);
            lineEnd = lineEnd < 0 ? markdown.Length : lineEnd;
            var isBlock = string.IsNullOrWhiteSpace(markdown[lineStart..match.Index]) && string.IsNullOrWhiteSpace(markdown[matchEnd..lineEnd]);

            if (isBlock)
            {
                builder.Append(markdown, position, lineStart - position).Append("\n\n").Append(content).Append("\n\n");
                position = lineEnd;
                continue;
            }

            builder.Append(markdown, position, match.Index - position).Append(content);
            position = matchEnd;
        }

        return builder.Append(markdown, position, markdown.Length - position).ToString();
    }

    private static async Task<string> LoadIncludeAsync(
        string includePath,
        string directory,
        HelpProcessingContext context,
        IReadOnlyList<string> stack,
        int depth,
        CancellationToken cancellationToken)
    {
        if (depth >= MaxIncludeDepth)
            return string.Empty;

        var path = HelpPaths.Resolve(directory, includePath);

        if (path is null || stack.Contains(path, StringComparer.OrdinalIgnoreCase))
            return string.Empty;

        var raw = await context.LoadFileAsync(path, cancellationToken);

        if (raw is null)
            return string.Empty;

        var cleaned = RemoveComments(ApplyMarkers(SplitFrontMatter(Normalize(raw)).Body));
        var expanded = await ExpandIncludesAsync(cleaned, HelpPaths.DirectoryOf(path), context, [.. stack, path], depth + 1, cancellationToken);

        return expanded.Trim();
    }

    private static string? ResolveImage(string url, HelpProcessingContext context)
    {
        var (path, suffix) = SplitSuffix(url);
        var resolved = HelpPaths.Resolve(HelpPaths.DirectoryOf(context.Topic + ".md"), path);

        return resolved is null ? null : context.ContentRootUrl + resolved + suffix;
    }

    private static MarkdownLink? ResolveLink(string url, HelpProcessingContext context)
    {
        if (url.StartsWith('#'))
            return null;

        var (path, suffix) = SplitSuffix(url);
        var resolved = HelpPaths.Resolve(HelpPaths.DirectoryOf(context.Topic + ".md"), path);

        if (resolved is null)
            return null;

        if (!resolved.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            return DocumentationLink(context, resolved + suffix);

        var topic = resolved[..^3];

        if (context.ResolveRoute?.Invoke(topic) is { } route)
            return new MarkdownLink(route.TrimStart('/') is { Length: > 0 } relative ? relative : "./");

        return new MarkdownLink(TopicLinkPrefix + topic, DataAttributes: new Dictionary<string, string> { [TopicLinkAttribute] = topic });
    }

    private static MarkdownLink? DocumentationLink(HelpProcessingContext context, string relative) =>
        CombineDocumentationUrl(context.DocumentationBaseUrl, context.Culture, relative) is { } url ? new MarkdownLink(url, OpenInNewTab: true) : null;

    private static string? CombineDocumentationUrl(string? baseUrl, string culture, string relative)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return null;

        return baseUrl.Replace("{culture}", culture, StringComparison.OrdinalIgnoreCase).TrimEnd('/') + "/" + relative;
    }

    private static (string Path, string Suffix) SplitSuffix(string url)
    {
        var index = url.IndexOfAny(['#', '?']);
        return index < 0 ? (url, string.Empty) : (url[..index], url[index..]);
    }

    [GeneratedRegex(@"\A\uFEFF?---[ \t]*\n(.*?)\n(?:---|\.\.\.)[ \t]*(?:\n|\z)", RegexOptions.Singleline)]
    private static partial Regex FrontMatter();

    [GeneratedRegex(@"^title:[ \t]*(.+?)[ \t]*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex FrontMatterTitle();

    [GeneratedRegex(@"<!--\s*(Docs-Only|Internal)-Start\s*-->.*?(?:<!--\s*\1-End\s*-->|\z)", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HiddenBlock();

    [GeneratedRegex(@"<!--\s*App-Only\b(.*?)-->", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AppOnly();

    [GeneratedRegex(@"<!--(?!\s*(?i:help:fields)\s*-->).*?-->", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"<!--\s*help:fields\s*-->", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FieldsMarker();

    [GeneratedRegex(@"^#[ \t]+(.+?)(?:[ \t]+#+)?[ \t]*$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"\[!INCLUDE\s*\[[^\]\n]*\]\(\s*([^)\s]+)\s*\)\s*\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IncludeTag();
}
