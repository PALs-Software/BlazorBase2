using BlazorBase.Components.Services;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.IssueReport;

/// <summary>Opens the <see cref="BaseIssueReportDialog"/>.</summary>
public interface IIssueReportService
{
    /// <summary>Shows the dialog and completes when the user closed it.</summary>
    Task OpenAsync(IssueReportDialogContent? content = null);
}

/// <summary>Default <see cref="IIssueReportService"/>; it needs the <c>FluentDialogProvider</c> that <c>BaseLayout</c> renders.</summary>
public sealed class IssueReportService(IDialogService dialogService, Microsoft.Extensions.Localization.IStringLocalizer<BaseIssueReportDialog> localizer) : IIssueReportService
{
    #region Injects

    private readonly IDialogService DialogService = dialogService;
    private readonly Microsoft.Extensions.Localization.IStringLocalizer<BaseIssueReportDialog> Localizer = localizer;

    #endregion

    /// <inheritdoc />
    public async Task OpenAsync(IssueReportDialogContent? content = null)
    {
        var parameters = new DialogParameters
        {
            Title = Localizer["ReportIssue"],
            PrimaryAction = string.Empty,
            SecondaryAction = string.Empty,
            ShowDismiss = true,
            PreventDismissOnOverlayClick = true,
            Width = "min(560px, calc(100vw - 1rem))",
        };

        var reference = await DialogService.ShowDialogAsync<BaseIssueReportDialog>(content ?? new IssueReportDialogContent(), parameters);
        await reference.Result;
    }
}
