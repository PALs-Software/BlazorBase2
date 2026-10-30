using BlazorBase.Components.Dialogs;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Services;

/// <summary>
/// Default <see cref="IConfirmationService"/> that shows a FluentUI confirmation dialog.
/// </summary>
public sealed class FluentUiConfirmationService(IDialogService dialogService) : IConfirmationService
{
    #region Injects

    private readonly IDialogService DialogService = dialogService;

    #endregion

    public Task<bool> ConfirmAsync(string message, string title, string confirmButtonText, string cancelButtonText)
        => ShowAsync(message, title, confirmButtonText, cancelButtonText, isDestructive: false);

    public Task<bool> ConfirmDestructiveAsync(string message, string title, string confirmButtonText, string cancelButtonText)
        => ShowAsync(message, title, confirmButtonText, cancelButtonText, isDestructive: true);

    private async Task<bool> ShowAsync(string message, string title, string confirmButtonText, string cancelButtonText, bool isDestructive)
    {
        var content = new ConfirmationDialogContent
        {
            Message = message,
            ConfirmButtonText = confirmButtonText,
            CancelButtonText = cancelButtonText,
            IsDestructive = isDestructive
        };

        var result = await DialogService.ShowConfirmationDialogAsync(title, content);
        return !result.Cancelled;
    }
}