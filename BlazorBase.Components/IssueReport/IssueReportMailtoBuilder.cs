using System.Globalization;

namespace BlazorBase.Components.IssueReport;

/// <summary>
/// Builds the <c>mailto:</c> link and the clipboard text of an issue report. It is a pure function of its
/// arguments. The link stays within <see cref="IssueReportOptions.MaxUrlLength"/> by shortening the diagnostics in
/// steps (shorter error texts, then dropping the least important lines last to first); the user's text is never cut.
/// </summary>
public static class IssueReportMailtoBuilder
{
    private const int ShortErrorLength = 100;
    private const string LineBreak = "\r\n";

    /// <summary>Builds the mail; pass <c>null</c> diagnostics to leave them out.</summary>
    public static IssueReportMail Build(IssueReportOptions options, IssueReportInput input, IssueReportDiagnostics? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(input);

        var recipient = options.Recipient.Trim();
        var summary = SingleLine(input.Summary);
        var subjectSummary = Shorten(summary, Math.Max(10, options.MaxSubjectSummaryLength));
        var subject = BuildSubject(options, input.Category, subjectSummary);
        var userSections = BuildUserSections(options, input, summary, subjectSummary.Length != summary.Length);

        var fullLines = diagnostics is null ? [] : DiagnosticLines(diagnostics, options.MaxErrorEntries, null);
        var shortLines = diagnostics is null ? [] : DiagnosticLines(diagnostics, options.MaxErrorEntries, ShortErrorLength);

        var candidates = new List<IReadOnlyList<string>> { fullLines };
        if (shortLines.Count > 0)
        {
            for (var count = shortLines.Count; count >= 0; count--)
                candidates.Add(shortLines.Take(count).ToList());
        }

        string? body = null;
        string? uri = null;
        var chosen = -1;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidateBody = ComposeBody(options, userSections, candidates[index], LineBreak);
            var candidateUri = ComposeUri(recipient, subject, candidateBody);
            body = candidateBody;
            uri = candidateUri;
            chosen = index;
            if (candidateUri.Length <= options.MaxUrlLength)
                break;
        }

        var exceeds = uri!.Length > options.MaxUrlLength;
        var fullBody = ComposeBody(options, userSections, fullLines, "\n");

        return new IssueReportMail
        {
            Recipient = recipient,
            Subject = subject,
            Body = body!,
            MailtoUri = uri,
            FullText = $"To: {recipient}\nSubject: {subject}\n\n{fullBody}",
            IsTruncated = fullLines.Count > 0 && chosen > 0,
            ExceedsLimit = exceeds,
        };
    }

    /// <summary>The complete diagnostics as key=value lines, as shown in the dialog's preview.</summary>
    public static string FormatDiagnostics(IssueReportDiagnostics diagnostics, int maxErrorEntries = 3)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return string.Join('\n', DiagnosticLines(diagnostics, maxErrorEntries, null));
    }

    private static string BuildSubject(IssueReportOptions options, string? category, string summary)
    {
        var prefix = options.SubjectPrefix ?? (string.IsNullOrWhiteSpace(options.SiteName) ? null : $"[{options.SiteName.Trim()}]");
        var categoryText = string.IsNullOrWhiteSpace(category) ? null : $"{SingleLine(category)}:";

        return string.Join(' ', new[] { prefix, categoryText, summary }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static List<string> BuildUserSections(IssueReportOptions options, IssueReportInput input, string summary, bool summaryWasShortened)
    {
        var sections = new List<string>();
        if (summaryWasShortened)
            sections.Add($"Summary: {summary}");

        var description = input.Description?.Trim();
        if (!string.IsNullOrEmpty(description))
            sections.Add($"{options.DescriptionHeading}:\n{description}");

        return sections;
    }

    private static string ComposeBody(IssueReportOptions options, List<string> userSections, IReadOnlyList<string> lines, string newLine)
    {
        var sections = new List<string>(userSections);
        if (lines.Count > 0)
            sections.Add($"{options.DiagnosticsHeading}:\n{string.Join('\n', lines)}");

        var text = string.Join("\n\n", sections).Replace("\r\n", "\n").Replace('\r', '\n');

        return newLine == "\n" ? text : text.Replace("\n", newLine);
    }

    private static string ComposeUri(string recipient, string subject, string body) =>
        $"mailto:{recipient}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";

    private static List<string> DiagnosticLines(IssueReportDiagnostics diagnostics, int maxErrorEntries, int? errorLimit)
    {
        var lines = new List<string>();
        Add(lines, "Page", diagnostics.PageUrl);
        Add(lines, "Time", diagnostics.TimeUtc?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        Add(lines, "App", diagnostics.App);
        Add(lines, "Correlation", diagnostics.CorrelationId);
        Add(lines, "User", diagnostics.User);

        var errors = diagnostics.Errors.TakeLast(Math.Max(0, maxErrorEntries)).Reverse().ToList();
        for (var index = 0; index < errors.Count; index++)
            Add(lines, $"Error{index + 1}", errorLimit is { } limit ? Shorten(SingleLine(errors[index]), limit) : SingleLine(errors[index]));

        Add(lines, "Route", diagnostics.Route);
        Add(lines, "Culture", diagnostics.Culture);
        Add(lines, "Theme", diagnostics.Theme);
        Add(lines, "Browser", diagnostics.Browser);

        return lines;
    }

    private static void Add(List<string> lines, string key, string? value)
    {
        var text = value is null ? string.Empty : SingleLine(value);
        if (text.Length > 0)
            lines.Add($"{key}={text}");
    }

    private static string SingleLine(string value) =>
        string.Join(' ', value.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Trim();

    private static string Shorten(string value, int limit) =>
        value.Length <= limit ? value : string.Concat(value.AsSpan(0, limit - 1), "…");
}
