namespace BlazorBase.Components.IssueReport;

/// <summary>Settings of the issue report dialog and its mail link.</summary>
public sealed class IssueReportOptions
{
    /// <summary>The mail address the report goes to, e.g. <c>support@example.com</c>. Required.</summary>
    public string Recipient { get; set; } = string.Empty;

    /// <summary>The name of the website; it is part of the subject prefix and of the diagnostics.</summary>
    public string SiteName { get; set; } = string.Empty;

    /// <summary>Overrides the subject prefix; by default <c>[SiteName]</c> is used.</summary>
    public string? SubjectPrefix { get; set; }

    /// <summary>The application version shown in the diagnostics; the entry assembly's version when null.</summary>
    public string? AppVersion { get; set; }

    /// <summary>The categories offered in the dialog; the field is left out when the list is empty.</summary>
    public IList<string> Categories { get; set; } = ["Bug", "Question", "Suggestion"];

    /// <summary>The longest <c>mailto:</c> link; diagnostics are shortened to stay within it, the user's text never is.</summary>
    public int MaxUrlLength { get; set; } = 1800;

    /// <summary>The most recent client errors listed in the report.</summary>
    public int MaxErrorEntries { get; set; } = 3;

    /// <summary>The longest subject text of the summary; the full summary is repeated in the body when it was cut.</summary>
    public int MaxSubjectSummaryLength { get; set; } = 100;

    /// <summary>Heading of the user's description in the mail body; mails are read by the support team, so it is not localized.</summary>
    public string DescriptionHeading { get; set; } = "Description";

    /// <summary>Heading of the diagnostics block in the mail body.</summary>
    public string DiagnosticsHeading { get; set; } = "Diagnostics";

    /// <summary>Registers a header button (bug icon) that opens the dialog.</summary>
    public bool RegisterHeaderAction { get; set; } = true;

    /// <summary>The <see cref="HeaderActions.HeaderActionItem.Order"/> of the header button.</summary>
    public int HeaderActionOrder { get; set; } = 100;
}
