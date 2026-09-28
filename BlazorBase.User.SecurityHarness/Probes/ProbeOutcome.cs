namespace BlazorBase.User.SecurityHarness.Probes;

/// <summary>What a single probe run concluded.</summary>
public enum ProbeOutcome
{
    /// <summary>The expectation held; this is also a result the report records, not only a failure.</summary>
    Passed,

    /// <summary>The expectation did not hold - a finding, at <see cref="IAuthProbe.SeverityIfFailed"/>.</summary>
    Failed,

    /// <summary>The probe could not establish either outcome and says so rather than guessing.</summary>
    Inconclusive,

    /// <summary>The probe deliberately did not run - its <see cref="IAuthProbe.Profile"/> did not start.</summary>
    Skipped,
}
