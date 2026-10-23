using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// The single source of truth for the shape of a dialog that hosts a card. Every framework dialog opener uses
/// it, and a host opening its own card dialog calls <see cref="Create"/> to get exactly the same look.
/// </summary>
public static class CardDialogDefaults
{
    /// <summary>The smallest width a card dialog asks for on a window that allows it.</summary>
    public const string MinimumWidth = "600px";

    /// <summary>The preferred width as a share of the viewport, so a card grows with a wide window.</summary>
    public const string PreferredWidth = "62vw";

    /// <summary>The largest width a card dialog grows to.</summary>
    public const string MaximumWidth = "1100px";

    /// <summary>The distance a card dialog keeps to the viewport edges, so it never exceeds the window.</summary>
    public const string ViewportMargin = "2rem";

    /// <summary>
    /// The CSS width of a card dialog: <c>clamp(600px, 62vw, 1100px)</c>, capped by the viewport minus a margin.
    /// A phone gets the viewport width minus the margin, a tablet roughly 600 px to 700 px, a wide desktop window
    /// up to 1100 px, which lets the card switch to its three-column layout.
    /// </summary>
    public const string Width = $"min(calc(100vw - {ViewportMargin}), clamp({MinimumWidth}, {PreferredWidth}, {MaximumWidth}))";

    /// <summary>
    /// Creates the dialog parameters of a card dialog: no title bar of its own (the card renders its header),
    /// no primary or secondary action (the card renders its buttons), a fluid <see cref="Width"/>, no dismiss
    /// by a click on the overlay and no dismiss button.
    /// </summary>
    /// <remarks>
    /// FluentUI's own focus trap stays off. A card can raise a second modal dialog on top of itself (the discard
    /// prompt), and two traps each pull focus back into their own dialog and fight over it. The card's dialog
    /// content instead makes everything outside itself inert, which holds focus inside without any cooperation
    /// between the dialogs.
    /// </remarks>
    /// <returns>A new <see cref="DialogParameters"/> instance the caller may adjust.</returns>
    public static DialogParameters Create() => new()
    {
        Title = string.Empty,
        ShowTitle = false,
        PrimaryAction = string.Empty,
        SecondaryAction = string.Empty,
        Width = Width,
        PreventDismissOnOverlayClick = true,
        ShowDismiss = false,
        TrapFocus = false
    };
}
