namespace BlazorBase.Components.Html;

/// <summary>Where a relative Markdown link points after <see cref="TrustedMarkdownOptions.ResolveLinkUrl"/> rewrote it.</summary>
/// <param name="Url">The URL the rendered anchor gets.</param>
/// <param name="OpenInNewTab">Whether the anchor opens in a new browser tab.</param>
public sealed record MarkdownLink(string Url, bool OpenInNewTab = false);
