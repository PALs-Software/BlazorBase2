using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.HeaderActions;

/// <summary>
/// One plain icon button in the layout header, shown in front of the help, theme and settings buttons.
/// </summary>
public sealed class HeaderActionItem
{
    /// <summary>A stable identifier; an item with the same id replaces an earlier one.</summary>
    public required string Id { get; init; }

    /// <summary>The icon, normally a 20 px regular icon.</summary>
    public required Icon Icon { get; init; }

    /// <summary>The localized title; it is the tooltip and the accessible name.</summary>
    public required string Title { get; init; }

    /// <summary>What the button does when it is activated.</summary>
    public required Func<IServiceProvider, Task> OnInvoke { get; init; }

    /// <summary>The position among the header actions; lower values come first, ties are ordered by <see cref="Id"/>.</summary>
    public int Order { get; init; }

    /// <summary>
    /// Roles of which the user needs at least one (comma separated). Without it any user sees the item; with it
    /// anonymous users never do.
    /// </summary>
    public string? RequiredRole { get; init; }

    /// <summary>An authorization policy the user must satisfy; a missing authorization service hides the item.</summary>
    public string? RequiredPolicy { get; init; }

    /// <summary>Evaluated before every render; the item is hidden while it returns false.</summary>
    public Func<IServiceProvider, bool>? IsVisible { get; init; }

    /// <summary>Marks the button as pressed (<c>aria-pressed</c>), e.g. while the panel it toggles is open.</summary>
    public Func<IServiceProvider, bool>? IsActive { get; init; }

    /// <summary>The <c>aria-haspopup</c> value when the button opens a dialog or panel, e.g. <c>dialog</c>.</summary>
    public string? PopupKind { get; init; }
}
