namespace BlazorBase.Components.Html;

/// <summary>Where a relative Markdown link points after <see cref="TrustedMarkdownOptions.ResolveLinkUrl"/> rewrote it.</summary>
/// <param name="Url">The URL the rendered anchor gets.</param>
/// <param name="OpenInNewTab">Whether the anchor opens in a new browser tab.</param>
/// <param name="DataAttributes">
/// Additional <c>data-*</c> attributes of the rendered anchor, e.g. for a script that handles the click. Entries
/// whose name is not <c>data-</c> followed by lowercase letters, digits and hyphens are ignored; values are HTML-encoded.
/// </param>
public sealed record MarkdownLink(string Url, bool OpenInNewTab = false, IReadOnlyDictionary<string, string>? DataAttributes = null);
