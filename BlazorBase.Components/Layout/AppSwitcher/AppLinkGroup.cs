namespace BlazorBase.Components.Layout.AppSwitcher;

/// <summary>
/// A titled group of <see cref="AppLink"/> entries in the <see cref="BaseAppSwitcher"/> popover.
/// </summary>
public sealed record AppLinkGroup
{
    /// <summary>Stable identifier of the group.</summary>
    public required string Key { get; init; }

    /// <summary>The group title, shown in capitals above the links.</summary>
    public required string Label { get; init; }

    /// <summary>The links of the group, in display order.</summary>
    public IReadOnlyList<AppLink> Links { get; init; } = [];

    /// <summary>
    /// Comma separated roles of which the user needs at least one to see the group; null shows it to everybody.
    /// A group without any visible link is not shown at all.
    /// </summary>
    public string? RequiredRole { get; init; }
}
