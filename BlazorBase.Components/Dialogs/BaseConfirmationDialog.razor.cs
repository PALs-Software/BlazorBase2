using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Dialogs;

/// <summary>
/// A two-button confirmation. Unlike FluentUI's built-in confirmation it can mark the action as
/// destructive: warning icon and a danger-styled confirm button.
/// </summary>
public partial class BaseConfirmationDialog : ComponentBase, IDialogContentComponent<ConfirmationDialogContent>
{
    [Parameter]
    public ConfirmationDialogContent Content { get; set; } = default!;

    [CascadingParameter]
    private FluentDialog? Dialog { get; set; }

    private async Task ConfirmAsync()
    {
        if (Dialog is not null)
            await Dialog.CloseAsync();
    }

    private async Task CancelAsync()
    {
        if (Dialog is not null)
            await Dialog.CancelAsync();
    }
}
