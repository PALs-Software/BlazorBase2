namespace BlazorBase.User.SecurityHarness.Probes;

/// <summary>The result of running one <see cref="IAuthProbe"/> once.</summary>
public sealed record ProbeResult(ProbeOutcome Outcome, string Summary, IReadOnlyList<ProbeEvidence> Evidence)
{
    public static ProbeResult Passed(string summary, params IReadOnlyList<ProbeEvidence> evidence) =>
        new(ProbeOutcome.Passed, summary, evidence);

    public static ProbeResult Failed(string summary, params IReadOnlyList<ProbeEvidence> evidence) =>
        new(ProbeOutcome.Failed, summary, evidence);

    public static ProbeResult Inconclusive(string summary, params IReadOnlyList<ProbeEvidence> evidence) =>
        new(ProbeOutcome.Inconclusive, summary, evidence);

    public static ProbeResult Skipped(string summary) =>
        new(ProbeOutcome.Skipped, summary, []);
}
