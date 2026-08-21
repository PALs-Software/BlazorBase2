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

    public async Task<bool> ConfirmAsync(string message, string title, string confirmButtonText, string cancelButtonText)
    {
        var dialog = await DialogService.ShowConfirmationAsync(message, confirmButtonText, cancelButtonText, title);
        var result = await dialog.Result;
        return !result.Cancelled;
    }
}
