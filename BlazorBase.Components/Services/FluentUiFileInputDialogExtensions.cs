using BlazorBase.Components.Dialogs;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Services;

/// <summary>
/// Opens the generic <see cref="BaseFileInputDialog"/> and <see cref="BaseDateInputDialog"/> the same
/// way FluentUI's own <c>ShowConfirmationAsync</c> opens its built-in confirmation dialog, so a page
/// action can offer a file upload or a single-date pick without hand-rolling a dialog component.
/// </summary>
public static class FluentUiFileInputDialogExtensions
{
    public static Task<IDialogReference> ShowFileInputDialogAsync(this IDialogService dialogService, string title, FileInputDialogContent content)
    {
        var dialogParameters = new DialogParameters
        {
            Title = title,
            PrimaryAction = string.Empty,
            SecondaryAction = string.Empty,
            ShowDismiss = false,
            PreventDismissOnOverlayClick = true,
            Width = "500px"
        };

        return dialogService.ShowDialogAsync<BaseFileInputDialog>(content, dialogParameters);
    }

    public static Task<IDialogReference> ShowDateInputDialogAsync(this IDialogService dialogService, string title, DateInputDialogContent content)
    {
        var dialogParameters = new DialogParameters
        {
            Title = title,
            PrimaryAction = string.Empty,
            SecondaryAction = string.Empty,
            ShowDismiss = false,
            PreventDismissOnOverlayClick = true,
            Width = "400px"
        };

        return dialogService.ShowDialogAsync<BaseDateInputDialog>(content, dialogParameters);
    }
}
