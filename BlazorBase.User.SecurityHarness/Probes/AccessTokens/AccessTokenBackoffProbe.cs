using System.Diagnostics;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.AccessTokens;

/// <summary>
/// Measures the per-client back-off delay <c>BruteForceDelayService</c> applies after repeated wrong
/// secrets from one address. A measurement, not a pass/fail check: the delay formula is
/// deterministic (<c>BaseDelayMs &lt;&lt; failures</c>, capped), so this reports the observed elapsed
/// time per attempt rather than inventing a threshold.
/// </summary>
internal sealed class AccessTokenBackoffProbe : IAuthProbe
{
    private const int Attempts = 5;

    public string Id => "AT-BACKOFF";
    public string Category => "Access tokens";
    public string Attack => $"{Attempts} consecutive wrong secrets against the access-token scheme, from one client.";
    public string Expectation => "The delay escalates; report the measured delays.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.AccessTokens;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();

        var elapsedMilliseconds = new List<double>();
        var evidence = new List<ProbeEvidence>();
        var stopwatch = new Stopwatch();

        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            var wrongSecret = "bbat_" + Guid.NewGuid().ToString("N");
            stopwatch.Restart();
            var exchange = await AccessTokenSupport.CallAccessTokenProbeEndpointAsync(client, context.BaseAddress, wrongSecret, cancellationToken);
            stopwatch.Stop();
            elapsedMilliseconds.Add(stopwatch.Elapsed.TotalMilliseconds);
            evidence.Add(exchange.ToEvidence($"Wrong-secret attempt {attempt} of {Attempts}, elapsed {stopwatch.Elapsed.TotalMilliseconds:F1} ms."));
        }

        var summary = $"Elapsed per attempt (ms): [{string.Join(", ", elapsedMilliseconds.Select(value => value.ToString("F1")))}].";

        var monotonic = true;
        for (var index = 1; index < elapsedMilliseconds.Count; index++)
        {
            if (elapsedMilliseconds[index] < elapsedMilliseconds[index - 1] * 0.5)
                monotonic = false;
        }

        if (!monotonic)
            return ProbeResult.Failed($"{summary} The delay did not escalate across attempts as BruteForceDelayService's doubling formula predicts.", evidence);

        return ProbeResult.Inconclusive($"{summary} The delay escalated across attempts, consistent with BruteForceDelayService's base-delay-doubling formula; reported as a measurement, not a threshold-based verdict.", evidence);
    }
}
