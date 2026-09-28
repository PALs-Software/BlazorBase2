namespace BlazorBase.User.SecurityHarness.Probes;

/// <summary>
/// One request/response pair (or other observation) that backs a <see cref="ProbeResult"/> - so a
/// failing probe is never a claim without something to show for it.
/// </summary>
public sealed record ProbeEvidence(string Description, string? Request = null, string? Response = null);
