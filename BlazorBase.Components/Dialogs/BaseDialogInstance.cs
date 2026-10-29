using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Dialogs;

/// <summary>
/// Base class of the content component of a dialog opened with <see cref="BaseDialogServiceExtensions"/>. It gives
/// the content the <see cref="FluentDialogInstance.DialogInstance"/> of its dialog and the three ways to finish it.
/// </summary>
/// <remarks>
/// The content renders a <see cref="BaseDialogBody"/>, which draws the title from the options and the footer from the
/// content. The built-in Fluent footer buttons are off, because every BlazorBase dialog places its own buttons in the
/// body's action area; <see cref="UseDefaultFooter"/> turns them back on for a dialog that wants them.
/// </remarks>
public abstract class BaseDialogInstance : FluentDialogInstance
{
    /// <summary>Shows Fluent's own OK and Cancel buttons in the footer. Off by default.</summary>
    protected virtual bool UseDefaultFooter => false;

    /// <summary>Closes the dialog with an accepted, empty result.</summary>
    protected Task ConfirmAsync() => DialogInstance.CloseAsync();

    /// <summary>Closes the dialog with an accepted result that carries <paramref name="result"/>.</summary>
    protected Task ConfirmWithResultAsync<TResult>(TResult result) => DialogInstance.CloseAsync(result);

    /// <summary>Closes the dialog with a cancelled result.</summary>
    protected Task CancelAsync() => DialogInstance.CancelAsync();

    protected override void OnInitializeDialog(DialogOptionsHeader header, DialogOptionsFooter footer)
    {
        if (UseDefaultFooter)
            return;

        footer.PrimaryAction.Visible = false;
        footer.SecondaryAction.Visible = false;
    }

    protected override Task OnActionClickedAsync(bool primary) => primary ? ConfirmAsync() : CancelAsync();
}

/// <summary>
/// A <see cref="BaseDialogInstance"/> whose configuration arrives as one <see cref="Content"/> object.
/// </summary>
/// <typeparam name="TContent">The configuration the caller passes to <c>ShowBaseDialogAsync</c>.</typeparam>
public abstract class BaseDialogInstance<TContent> : BaseDialogInstance
    where TContent : notnull
{
    /// <summary>The configuration passed by the caller.</summary>
    [Parameter]
    public TContent Content { get; set; } = default!;
}
