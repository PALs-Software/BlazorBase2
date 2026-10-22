namespace BlazorBase.Components.Help;

/// <summary>Everything <see cref="HelpMarkdownProcessor"/> needs to know about the document it processes.</summary>
public sealed class HelpProcessingContext
{
    /// <summary>The topic of the document, e.g. <c>general/blog</c>; relative links, images and includes are resolved from its folder.</summary>
    public required string Topic { get; init; }

    /// <summary>Where the culture folder of the document is served from, ending in <c>/</c>, e.g. <c>help/en-US/</c>; images are loaded from below it.</summary>
    public required string ContentRootUrl { get; init; }

    /// <summary>Loads a file of the culture folder by its path (e.g. <c>includes/list-basics.md</c>); returns null when it does not exist.</summary>
    public required Func<string, CancellationToken, Task<string?>> LoadFileAsync { get; init; }

    /// <summary>
    /// Maps a topic to the route of the application page that shows it. A link to a topic with a route navigates to the
    /// page; a null result (a topic without a page, e.g. a category topic) makes the link open inside the help panel.
    /// </summary>
    public Func<string, string?>? ResolveRoute { get; init; }

    /// <summary>The documentation site's address, see <see cref="HelpOptions.DocumentationBaseUrl"/>; null or empty turns links to other files (not topics) into plain text.</summary>
    public string? DocumentationBaseUrl { get; init; }

    /// <summary>The culture folder of the document, substituted for <c>{culture}</c> in <see cref="DocumentationBaseUrl"/>.</summary>
    public string Culture { get; init; } = string.Empty;

    /// <summary>The titles of the callouts by alert name (<c>NOTE</c>, <c>TIP</c>, <c>IMPORTANT</c>, <c>WARNING</c>, <c>CAUTION</c>).</summary>
    public IReadOnlyDictionary<string, string>? AlertTitles { get; init; }
}
