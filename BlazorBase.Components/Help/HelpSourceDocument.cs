namespace BlazorBase.Components.Help;

/// <summary>The Markdown of a help topic as the content source found it.</summary>
/// <param name="Topic">The topic the content belongs to; the fallback topic when the requested one has no content.</param>
/// <param name="Culture">The culture folder the content was read from; the default culture when the current one has none.</param>
/// <param name="Markdown">The raw Markdown of the file.</param>
public sealed record HelpSourceDocument(string Topic, string Culture, string Markdown);
