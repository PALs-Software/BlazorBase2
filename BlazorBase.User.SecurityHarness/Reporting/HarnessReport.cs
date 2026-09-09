using BlazorBase.User.SecurityHarness.Probes;

namespace BlazorBase.User.SecurityHarness.Reporting;

/// <summary>Whether and where one <see cref="Hosting.HarnessProfile"/>'s host ended up.</summary>
public sealed record HarnessHostReportEntry(
    string Profile,
    string Description,
    string Status,
    string? BaseAddress,
    string? FailureMessage);

/// <summary>One probe's result, flattened for rendering.</summary>
public sealed record ProbeReportEntry(
    string Id,
    string Category,
    string Attack,
    string Expectation,
    string Profile,
    string Severity,
    string Outcome,
    string Summary,
    IReadOnlyList<string> Evidence);

/// <summary>Everything one harness run produced: which hosts came up, and every probe that ran.</summary>
public sealed record HarnessReport(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<HarnessHostReportEntry> Hosts,
    IReadOnlyList<ProbeReportEntry> Probes)
{
    public int PassedCount => Probes.Count(probe => probe.Outcome == nameof(ProbeOutcome.Passed));
    public int FailedCount => Probes.Count(probe => probe.Outcome == nameof(ProbeOutcome.Failed));
    public int InconclusiveCount => Probes.Count(probe => probe.Outcome == nameof(ProbeOutcome.Inconclusive));
    public int SkippedCount => Probes.Count(probe => probe.Outcome == nameof(ProbeOutcome.Skipped));
}
