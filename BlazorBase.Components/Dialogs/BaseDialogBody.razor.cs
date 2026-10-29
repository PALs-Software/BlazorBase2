using Microsoft.AspNetCore.Components;

namespace BlazorBase.Components.Dialogs;

/// <summary>
/// The frame of a BlazorBase dialog: the title from the dialog options, the content, and an optional footer with the
/// dialog's buttons. Render it as the whole markup of a <see cref="BaseDialogInstance"/>.
/// </summary>
/// <remarks>
/// It adds the keyboard behavior of <see cref="DialogBehavior"/>. With <see cref="Flush"/> there is no frame at all:
/// the content fills the dialog edge to edge and brings its own header and action bar, as a card does.
/// </remarks>
public partial class BaseDialogBody : ComponentBase
{
    /// <summary>The content of the dialog.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>The buttons at the bottom, primary button first. Without it the dialog has no footer.</summary>
    [Parameter]
    public RenderFragment? ActionTemplate { get; set; }

    /// <summary>Renders the content without title, padding or footer.</summary>
    [Parameter]
    public bool Flush { get; set; }

    /// <summary>Keeps title and footer in place while only the content scrolls. On by default.</summary>
    [Parameter]
    public bool FixedHeaderFooter { get; set; } = true;

    /// <summary>Moves focus into the dialog when it opens. On by default.</summary>
    [Parameter]
    public bool InitialFocus { get; set; } = true;

    /// <summary>Commits the focused text field when Escape is pressed. On by default.</summary>
    [Parameter]
    public bool CommitPendingEditOnEscape { get; set; } = true;
}
