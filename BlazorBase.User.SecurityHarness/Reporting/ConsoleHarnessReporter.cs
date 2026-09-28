using System.Text;

namespace BlazorBase.User.SecurityHarness.Reporting;

/// <summary>Plain-text reporter for interactive runs; the default format.</summary>
public sealed class ConsoleHarnessReporter : IHarnessReporter
{
    public string Render(HarnessReport report)
    {
        var text = new StringBuilder();

        text.AppendLine($"BlazorBase.User.SecurityHarness report - {report.GeneratedAtUtc:u}");
        text.AppendLine();
        text.AppendLine("Host profiles:");

        foreach (var host in report.Hosts)
        {
            text.AppendLine($"  [{host.Status}] {host.Profile} - {host.Description}");

            if (host.BaseAddress is not null)
                text.AppendLine($"           {host.BaseAddress}");

            if (host.FailureMessage is not null)
                text.AppendLine($"           {host.FailureMessage}");
        }

        text.AppendLine();
        text.AppendLine($"Probes: {report.Probes.Count} total, {report.PassedCount} passed, {report.FailedCount} failed, " +
            $"{report.InconclusiveCount} inconclusive, {report.SkippedCount} skipped.");

        foreach (var probe in report.Probes)
        {
            text.AppendLine();
            text.AppendLine($"  [{probe.Outcome}] {probe.Id} ({probe.Category}, {probe.Profile}, severity if failed: {probe.Severity})");
            text.AppendLine($"    Attack:      {probe.Attack}");
            text.AppendLine($"    Expectation: {probe.Expectation}");
            text.AppendLine($"    Summary:     {probe.Summary}");

            foreach (var evidenceLine in probe.Evidence)
                text.AppendLine($"    Evidence:    {evidenceLine}");
        }

        return text.ToString();
    }
}
