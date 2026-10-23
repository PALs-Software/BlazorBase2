namespace BlazorBase.Components.Html;

/// <summary>
/// Loosens <see cref="MarkdownConverter"/> for Markdown that ships with the application — documentation
/// kept in the repository — and never for text written by users or a language model.
/// </summary>
/// <remarks>
/// Raw HTML stays escaped and only <c>http</c>, <c>https</c>, <c>mailto</c> and relative URLs survive, exactly as
/// in the default mode; what changes is that images are rendered, links can be rewritten and open in a new tab,
/// and DocFX alerts become callouts.
/// </remarks>
public sealed class TrustedMarkdownOptions
{
    /// <summary>Renders images instead of turning them into links carrying their alt text.</summary>
    public bool AllowImages { get; init; }

    /// <summary>Adds <c>target="_blank"</c> and <c>rel="noopener noreferrer"</c> to <c>http</c> and <c>https</c> links.</summary>
    public bool OpenExternalLinksInNewTab { get; init; }

    /// <summary>
    /// Maps the URL of an image to the URL to load it from; returning null renders the alt text instead. Without it
    /// relative and <c>http</c>/<c>https</c> URLs are kept as written.
    /// </summary>
    public Func<string, string?>? ResolveImageUrl { get; init; }

    /// <summary>
    /// Maps a relative link URL to its target; returning null renders the link text without a link. Without it
    /// relative links are kept as written.
    /// </summary>
    public Func<string, MarkdownLink?>? ResolveLinkUrl { get; init; }

    /// <summary>
    /// Turns DocFX alerts (<c>&gt; [!NOTE]</c>, <c>[!TIP]</c>, <c>[!IMPORTANT]</c>, <c>[!WARNING]</c>, <c>[!CAUTION]</c>)
    /// into callouts titled with the value stored under the alert's upper-case name; null leaves them as quotes.
    /// </summary>
    public IReadOnlyDictionary<string, string>? AlertTitles { get; init; }

    /// <summary>
    /// Called with the parsed document after links, images and alerts were rewritten and before it is rendered, so
    /// the application can add attributes (for example heading ids) to its blocks. The Markdown and the document stay
    /// as safe as without it; null does nothing.
    /// </summary>
    public Action<Markdig.Syntax.MarkdownDocument>? PrepareDocument { get; init; }
}
