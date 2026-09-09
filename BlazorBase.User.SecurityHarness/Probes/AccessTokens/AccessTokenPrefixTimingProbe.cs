using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.AccessTokens;

/// <summary>
/// Samples response latency for a wrong prefix (no candidates loaded, rejected immediately) against
/// a right-prefix-wrong-secret (candidates loaded, a hash comparison performed) - the same
/// statistical approach as <c>LOGIN-TIMING-ORACLE</c>: interleaved sampling reduced through
/// <see cref="TimingSideChannelAnalysis"/>'s bootstrap confidence interval.
/// </summary>
internal sealed class AccessTokenPrefixTimingProbe : IAuthProbe
{
    private const int SampleCount = 30;

    public string Id => "AT-PREFIX-TIMING";
    public string Category => "Access tokens";
    public string Attack => $"{SampleCount} timed requests with an unknown prefix, and {SampleCount} with a known prefix and a wrong secret.";
    public string Expectation => "Report the medians; a large gap tells an attacker when a prefix is real.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.AccessTokens;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var overrides = new Dictionary<string, string?>
        {
            ["AccessTokenSettings:BruteForceBaseDelayMs"] = "0",
            ["AccessTokenSettings:BruteForceMaxDelayMs"] = "0",
        };
        await using var host = await context.StartIsolatedHostAsync(overrides, cancellationToken);
        context = host.Context;

        var created = await AccessTokenSupport.CreateTokenAsync(context, "at-prefix-timing", null, null, cancellationToken);
        var knownPrefix = created.Secret[..9];

        using var client = context.HttpClientFactory();

        var (unknownPrefixSamples, knownPrefixSamples) = await InterleavedTimingSampler.SampleAsync(
            ct => AccessTokenSupport.CallAccessTokenProbeEndpointAsync(client, context.BaseAddress, "bbat_" + Guid.NewGuid().ToString("N")[..8], ct),
            ct => AccessTokenSupport.CallAccessTokenProbeEndpointAsync(client, context.BaseAddress, knownPrefix + Guid.NewGuid().ToString("N"), ct),
            SampleCount, cancellationToken);

        var verdict = TimingSideChannelAnalysis.Evaluate(unknownPrefixSamples, knownPrefixSamples);

        var summary =
            $"{SampleCount} samples each, interleaved. Unknown-prefix median {verdict.MedianA:F2} ms (IQR {verdict.InterquartileRangeA:F2} ms); " +
            $"known-prefix-wrong-secret median {verdict.MedianB:F2} ms (IQR {verdict.InterquartileRangeB:F2} ms); delta {verdict.Delta:F2} ms " +
            $"(99% bootstrap interval [{verdict.LowerBound:F2}, {verdict.UpperBound:F2}] ms).";

        var evidence = new ProbeEvidence(
            "Raw sample latencies, in milliseconds.",
            Request: $"GET harness/access-token-probe x{SampleCount} (unknown prefix) and x{SampleCount} (known prefix, wrong secret), interleaved one-for-one",
            Response: $"unknown-prefix: [{string.Join(", ", unknownPrefixSamples.Select(sample => sample.ToString("F2")))}]\n" +
                      $"known-prefix: [{string.Join(", ", knownPrefixSamples.Select(sample => sample.ToString("F2")))}]");

        if (verdict.SideChannelDemonstrated)
            return ProbeResult.Failed($"{summary} The interval excludes zero - a timing side channel is measurable.", evidence);

        return ProbeResult.Inconclusive($"{summary} The interval includes zero - no timing side channel demonstrated.", evidence);
    }
}
