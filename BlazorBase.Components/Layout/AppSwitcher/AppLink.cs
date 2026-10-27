namespace BlazorBase.Components.Layout.AppSwitcher;

/// <summary>
/// One website or application the <see cref="BaseAppSwitcher"/> offers a link to.
/// </summary>
public sealed record AppLink
{
    /// <summary>Stable identifier; compare it with <see cref="BaseAppSwitcher.CurrentKey"/> to mark the current app.</summary>
    public required string Key { get; init; }

    /// <summary>The name shown next to the icon.</summary>
    public required string Label { get; init; }

    /// <summary>The address the link points to.</summary>
    public required string Href { get; init; }

    /// <summary>Source of the icon shown in the tile and in the header chip; a neutral glyph is used when null.</summary>
    public string? IconSrc { get; init; }

    /// <summary>Optional second line below the name.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Comma separated roles of which the user needs at least one to see the link; null shows it to everybody.
    /// Evaluated like <c>NavigationItem.RequiredRole</c>.
    /// </summary>
    public string? RequiredRole { get; init; }

    /// <summary>Opens the link in a new browser tab (with <c>noopener noreferrer</c>); the default.</summary>
    public bool OpenInNewTab { get; init; } = true;
}
