using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes;

/// <summary>
/// One self-describing adversarial check against a single <see cref="HarnessProfile"/>. A probe
/// states the attack, the expected outcome and the severity if that expectation fails, so a finding
/// is never a bare claim.
/// </summary>
public interface IAuthProbe
{
    /// <summary>A stable identifier, e.g. <c>"EXT-ALG-NONE"</c>. Used for <c>--id</c> filtering.</summary>
    string Id { get; }

    /// <summary>The report grouping, e.g. <c>"External token exchange"</c>.</summary>
    string Category { get; }

    /// <summary>What is attempted, in one sentence.</summary>
    string Attack { get; }

    /// <summary>What a correct implementation does in response.</summary>
    string Expectation { get; }

    /// <summary>How severe it is when <see cref="Expectation"/> does not hold.</summary>
    ProbeSeverity SeverityIfFailed { get; }

    /// <summary>The host profile this probe attacks.</summary>
    HarnessProfile Profile { get; }

    Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken);
}
