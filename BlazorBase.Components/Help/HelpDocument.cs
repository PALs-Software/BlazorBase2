namespace BlazorBase.Components.Help;

/// <summary>A help document after <see cref="HelpMarkdownProcessor"/> prepared it for the panel.</summary>
/// <param name="Title">The title from the front matter, else the first heading; null when the document has neither.</param>
/// <param name="Markdown">The Markdown to render: markers removed, includes resolved, the title heading left out.</param>
/// <param name="FieldsStartLine">
/// The zero-based line of <see cref="Markdown"/> where the <c>&lt;!-- help:fields --&gt;</c> marker stood, or null
/// when the document has none. The list items from there up to the next level-2 heading are the field entries.
/// </param>
public sealed record HelpDocument(string? Title, string Markdown, int? FieldsStartLine = null);
