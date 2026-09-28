using System.Text;
using BlazorBase.User.SecurityHarness.Probes;

namespace BlazorBase.User.SecurityHarness.Reporting;

/// <summary>
/// Markdown reporter, grouped by <see cref="ProbeReportEntry.Category"/> with outcome, severity and
/// evidence shown for anything that did not pass, ending in a summary table.
/// </summary>
public sealed class MarkdownHarnessReporter : IHarnessReporter
{
    public string Render(HarnessReport report)
    {
        var text = new StringBuilder();

        text.AppendLine("# BlazorBase.User.SecurityHarness report");
        text.AppendLine();
        text.AppendLine($"Generated {report.GeneratedAtUtc:u}.");
        text.AppendLine();

        AppendHostSection(text, report);
        AppendProbeSections(text, report);
        AppendSummaryTable(text, report);

        return text.ToString();
    }

    private static void AppendHostSection(StringBuilder text, HarnessReport report)
    {
        text.AppendLine("## Host profiles");
        text.AppendLine();
        text.AppendLine("| Profile | Status | Address | Notes |");
        text.AppendLine("|---|---|---|---|");

        foreach (var host in report.Hosts)
        {
            var notes = host.FailureMessage ?? host.Description;
            text.AppendLine($"| {host.Profile} | {host.Status} | {host.BaseAddress ?? "-"} | {notes} |");
        }

        text.AppendLine();
    }

    private static void AppendProbeSections(StringBuilder text, HarnessReport report)
    {
        text.AppendLine("## Probes");
        text.AppendLine();

        if (report.Probes.Count == 0)
        {
            text.AppendLine("_No probes in the catalogue yet._");
            text.AppendLine();
            return;
        }

        foreach (var categoryGroup in report.Probes.GroupBy(probe => probe.Category).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            text.AppendLine($"### {categoryGroup.Key}");
            text.AppendLine();

            foreach (var probe in categoryGroup.OrderBy(probe => probe.Id, StringComparer.Ordinal))
            {
                text.AppendLine($"- **{probe.Id}** - {probe.Outcome} (severity if failed: {probe.Severity}, profile: {probe.Profile})");
                text.AppendLine($"  - Attack: {probe.Attack}");
                text.AppendLine($"  - Expectation: {probe.Expectation}");
                text.AppendLine($"  - Summary: {probe.Summary}");

                if (probe.Outcome != nameof(ProbeOutcome.Passed))
                {
                    foreach (var evidenceLine in probe.Evidence)
                        text.AppendLine($"  - Evidence: {evidenceLine}");
                }
            }

            text.AppendLine();
        }
    }

    private static void AppendSummaryTable(StringBuilder text, HarnessReport report)
    {
        text.AppendLine("## Summary");
        text.AppendLine();
        text.AppendLine("| Passed | Failed | Inconclusive | Skipped |");
        text.AppendLine("|---|---|---|---|");
        text.AppendLine($"| {report.PassedCount} | {report.FailedCount} | {report.InconclusiveCount} | {report.SkippedCount} |");
    }
}
