namespace BlazorBase.User.SecurityHarness.Probes;

/// <summary>How severe it is when a probe's expectation is not met.</summary>
public enum ProbeSeverity
{
    Info,
    Low,
    Medium,
    High,
    Critical,
}
