namespace BlazorBase.Components.IssueReport;

/// <summary>Optional pre-filled values of a <see cref="BaseIssueReportDialog"/>.</summary>
public sealed class IssueReportDialogContent
{
    /// <summary>The initial summary.</summary>
    public string? Summary { get; init; }

    /// <summary>The initial description.</summary>
    public string? Description { get; init; }

    /// <summary>The initially selected category; must be one of <see cref="IssueReportOptions.Categories"/>.</summary>
    public string? Category { get; init; }
}
