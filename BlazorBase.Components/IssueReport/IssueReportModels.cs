namespace BlazorBase.Components.IssueReport;

/// <summary>What the user typed into the issue dialog.</summary>
/// <param name="Summary">A one line summary; it becomes the subject.</param>
/// <param name="Description">The free text description; it is never shortened.</param>
/// <param name="Category">An optional category, e.g. <c>Bug</c>.</param>
public sealed record IssueReportInput(string Summary, string? Description = null, string? Category = null);

/// <summary>The technical context of an issue report; every value is optional.</summary>
public sealed record IssueReportDiagnostics
{
    /// <summary>The URL of the page the user was on.</summary>
    public string? PageUrl { get; init; }

    /// <summary>The route relative to the application base.</summary>
    public string? Route { get; init; }

    /// <summary>When the report was started.</summary>
    public DateTimeOffset? TimeUtc { get; init; }

    /// <summary>The site name and version, e.g. <c>dev.acadon 1.14.0</c>.</summary>
    public string? App { get; init; }

    /// <summary>The signed-in user, e.g. the mail address.</summary>
    public string? User { get; init; }

    /// <summary>The correlation id of the last server response.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>The UI culture name.</summary>
    public string? Culture { get; init; }

    /// <summary>The theme preference.</summary>
    public string? Theme { get; init; }

    /// <summary>The browser's user agent.</summary>
    public string? Browser { get; init; }

    /// <summary>The newest client errors, oldest first.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>The mail the builder produced for one report.</summary>
public sealed class IssueReportMail
{
    /// <summary>The recipient address.</summary>
    public required string Recipient { get; init; }

    /// <summary>The subject with the site prefix.</summary>
    public required string Subject { get; init; }

    /// <summary>The body as placed in the link, with CRLF line breaks.</summary>
    public required string Body { get; init; }

    /// <summary>The <c>mailto:</c> link.</summary>
    public required string MailtoUri { get; init; }

    /// <summary>The complete text for the clipboard: recipient, subject and body with all diagnostics.</summary>
    public required string FullText { get; init; }

    /// <summary>True when diagnostics were shortened or left out to keep the link within the limit.</summary>
    public bool IsTruncated { get; init; }

    /// <summary>True when even the user's text alone exceeds the limit; the link is still complete, but some mail programs may refuse it.</summary>
    public bool ExceedsLimit { get; init; }
}
