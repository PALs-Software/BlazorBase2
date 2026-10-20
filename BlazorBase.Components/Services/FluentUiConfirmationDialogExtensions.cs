using BlazorBase.Components.Dialogs;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Services;

/// <summary>
/// Opens the <see cref="BaseConfirmationDialog"/> through FluentUI's dialog service.
/// </summary>
public static class FluentUiConfirmationDialogExtensions
{
    /// <summary>
    /// Shows a confirmation and returns its reference; the result is cancelled when the user declined,
    /// dismissed it with Escape or clicked outside it.
    /// </summary>
    public static Task<IDialogReference> ShowConfirmationDialogAsync(this IDialogService dialogService, string title, ConfirmationDialogContent content)
    {
        var dialogParameters = new DialogParameters
        {
            Title = title,
            ShowTitle = true,
            PrimaryAction = string.Empty,
            SecondaryAction = string.Empty,
            ShowDismiss = false,
            PreventDismissOnOverlayClick = false,
            Width = "min(440px, calc(100vw - 2rem))"
        };

        return dialogService.ShowDialogAsync<BaseConfirmationDialog>(content, dialogParameters);
    }
}
