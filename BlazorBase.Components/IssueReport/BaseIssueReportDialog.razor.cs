using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.IssueReport;

/// <summary>
/// The dialog that turns a short description plus optional diagnostics into a <c>mailto:</c> link for the support
/// mailbox. The diagnostics are shown in full before anything is sent and can be switched off. When the mail program
/// does not open, the full text is copied to the clipboard instead. The dialog closes only on the user's action after
/// showing what happened. Open it with <see cref="IIssueReportService"/> or the registered header button.
/// </summary>
public partial class BaseIssueReportDialog : ComponentBase, IDialogContentComponent<IssueReportDialogContent>
{
    #region Injects

    [Inject]
    private IStringLocalizer<BaseIssueReportDialog> Localizer { get; set; } = default!;

    [Inject]
    private IOptions<IssueReportOptions> OptionsAccessor { get; set; } = default!;

    [Inject]
    private IIssueReportDiagnosticsCollector Collector { get; set; } = default!;

    [Inject]
    private IIssueReportBrowser Browser { get; set; } = default!;

    #endregion

    /// <summary>The pre-filled values.</summary>
    [Parameter]
    public IssueReportDialogContent Content { get; set; } = new();

    [CascadingParameter]
    private FluentDialog? Dialog { get; set; }

    #region Members

    private IssueReportOptions Options => OptionsAccessor.Value;
    private IssueReportDiagnostics Diagnostics = new();
    private string Summary = string.Empty;
    private string Description = string.Empty;
    private string Category = string.Empty;
    private bool IncludeDiagnostics = true;
    private IssueReportPhase Phase = IssueReportPhase.Form;
    private CopyOutcome CopyState = CopyOutcome.None;

    #endregion

    private enum IssueReportPhase
    {
        Form,
        Opened,
        Fallback,
    }

    private enum CopyOutcome
    {
        None,
        Copied,
        Failed,
    }

    private IReadOnlyList<string> CategoryItems = [string.Empty];

    private bool CanSubmit => !string.IsNullOrWhiteSpace(Summary) && !string.IsNullOrWhiteSpace(Options.Recipient);

    private string DiagnosticsText => IssueReportMailtoBuilder.FormatDiagnostics(Diagnostics, Options.MaxErrorEntries);

    private IssueReportMail Mail => IssueReportMailtoBuilder.Build(
        Options,
        new IssueReportInput(Summary, Description, string.IsNullOrEmpty(Category) ? null : Category),
        IncludeDiagnostics ? Diagnostics : null);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        Summary = Content.Summary ?? string.Empty;
        Description = Content.Description ?? string.Empty;
        Category = Content.Category ?? string.Empty;
        CategoryItems = [string.Empty, .. Options.Categories];
        Diagnostics = await Collector.CollectAsync();
    }

    private async Task OpenMailAsync()
    {
        if (!CanSubmit)
            return;

        var mail = Mail;
        if (await Browser.OpenMailtoAsync(mail.MailtoUri))
        {
            Phase = IssueReportPhase.Opened;
            return;
        }

        CopyState = await Browser.CopyTextAsync(mail.FullText) ? CopyOutcome.Copied : CopyOutcome.Failed;
        Phase = IssueReportPhase.Fallback;
    }

    private async Task CopyDetailsAsync() =>
        CopyState = await Browser.CopyTextAsync(Mail.FullText) ? CopyOutcome.Copied : CopyOutcome.Failed;

    private async Task CancelAsync()
    {
        if (Dialog is not null)
            await Dialog.CancelAsync();
    }

    private async Task CloseAsync()
    {
        if (Dialog is not null)
            await Dialog.CloseAsync();
    }
}
