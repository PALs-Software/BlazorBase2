using BlazorBase.Components.Dialogs;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Services;

/// <summary>
/// Opens the <see cref="BaseConfirmationDialog"/>.
/// </summary>
public static class FluentUiConfirmationDialogExtensions
{
    /// <summary>
    /// Shows a confirmation and returns when it is closed; the result is cancelled when the user declined,
    /// dismissed it with Escape or clicked outside it.
    /// </summary>
    public static Task<DialogResult> ShowConfirmationDialogAsync(this IDialogService dialogService, string title, ConfirmationDialogContent content) =>
        dialogService.ShowBaseDialogAsync<BaseConfirmationDialog, ConfirmationDialogContent>(title, content, "min(440px, calc(100vw - 2rem))");
}
