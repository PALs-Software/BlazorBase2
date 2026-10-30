namespace BlazorBase.Components.Help;

/// <summary>An entry of the fields section of a help document: a top-level list item whose text starts with a bold label.</summary>
/// <param name="Label">The bold label as written, e.g. <c>Monthly budget</c>.</param>
/// <param name="Key">The normalized label, see <see cref="HelpLabelKey.Normalize"/>.</param>
/// <param name="Id">The <c>id</c> of the rendered list item: <c>field-</c> plus the slug of the label, unique in the document.</param>
/// <param name="ShortHtml">The first sentence of the entry as safe inline HTML (bold, italic, strikethrough and code; links as plain text); empty when the entry has no text.</param>
public sealed record HelpFieldEntry(string Label, string Key, string Id, string ShortHtml);
