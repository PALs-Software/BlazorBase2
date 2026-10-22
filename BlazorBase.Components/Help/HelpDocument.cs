namespace BlazorBase.Components.Help;

/// <summary>A help document after <see cref="HelpMarkdownProcessor"/> prepared it for the panel.</summary>
/// <param name="Title">The title from the front matter, else the first heading; null when the document has neither.</param>
/// <param name="Markdown">The Markdown to render: markers removed, includes resolved, the title heading left out.</param>
public sealed record HelpDocument(string? Title, string Markdown);
