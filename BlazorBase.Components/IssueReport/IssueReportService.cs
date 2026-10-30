using BlazorBase.Components.Dialogs;
using BlazorBase.Components.Services;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.IssueReport;

/// <summary>Opens the <see cref="BaseIssueReportDialog"/>.</summary>
public interface IIssueReportService
{
    /// <summary>Shows the dialog and completes when the user closed it.</summary>
    Task OpenAsync(IssueReportDialogContent? content = null);
}

/// <summary>Default <see cref="IIssueReportService"/>; it needs the dialog provider that <c>BaseLayout</c> renders.</summary>
public sealed class IssueReportService(IDialogService dialogService, Microsoft.Extensions.Localization.IStringLocalizer<BaseIssueReportDialog> localizer) : IIssueReportService
{
    #region Injects

    private readonly IDialogService DialogService = dialogService;
    private readonly Microsoft.Extensions.Localization.IStringLocalizer<BaseIssueReportDialog> Localizer = localizer;

    #endregion

    /// <inheritdoc />
    public async Task OpenAsync(IssueReportDialogContent? content = null)
    {
        await DialogService.ShowBaseDialogAsync<BaseIssueReportDialog>(options =>
        {
            options.Title = Localizer["ReportIssue"];
            options.Width = "min(560px, calc(100vw - 1rem))";
            options.ShowCloseButton = true;
            options.CloseButtonLabel = Localizer["Close"];
            options.Parameters[nameof(BaseIssueReportDialog.Content)] = content ?? new IssueReportDialogContent();
        });
    }
}
