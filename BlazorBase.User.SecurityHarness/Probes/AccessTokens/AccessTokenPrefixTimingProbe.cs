using System.Diagnostics;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.AccessTokens;

/// <summary>
/// Samples response latency for a wrong prefix (no candidates loaded, rejected immediately) against
/// a right-prefix-wrong-secret (candidates loaded, a hash comparison performed) - the same
/// statistical approach as <c>LOGIN-TIMING-ORACLE</c>. Reports both medians and their delta, and only
/// calls it a finding when the delta clearly exceeds the noise in both distributions.
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

        var unknownPrefixSamples = await SampleAsync(client, context.BaseAddress, () => "bbat_" + Guid.NewGuid().ToString("N")[..8], cancellationToken);
        var knownPrefixSamples = await SampleAsync(client, context.BaseAddress, () => knownPrefix + Guid.NewGuid().ToString("N"), cancellationToken);

        var (unknownMedian, unknownIqr) = MedianAndInterquartileRange(unknownPrefixSamples);
        var (knownMedian, knownIqr) = MedianAndInterquartileRange(knownPrefixSamples);
        var delta = Math.Abs(knownMedian - unknownMedian);
        var noiseFloor = Math.Max(knownIqr, unknownIqr);

        var summary =
            $"{SampleCount} samples each. Unknown-prefix median {unknownMedian:F2} ms (IQR {unknownIqr:F2} ms); " +
            $"known-prefix-wrong-secret median {knownMedian:F2} ms (IQR {knownIqr:F2} ms); delta {delta:F2} ms.";

        var evidence = new ProbeEvidence(
            "Raw sample latencies, in milliseconds.",
            Request: $"GET harness/access-token-probe x{SampleCount} (unknown prefix) and x{SampleCount} (known prefix, wrong secret)",
            Response: $"unknown-prefix: [{string.Join(", ", unknownPrefixSamples.Select(sample => sample.ToString("F2")))}]\n" +
                      $"known-prefix: [{string.Join(", ", knownPrefixSamples.Select(sample => sample.ToString("F2")))}]");

        if (delta > noiseFloor)
            return ProbeResult.Failed($"{summary} The delta exceeds the noise floor (max IQR {noiseFloor:F2} ms) - a timing side channel is measurable.", evidence);

        return ProbeResult.Inconclusive($"{summary} The delta sits inside the noise (max IQR {noiseFloor:F2} ms) - no timing side channel demonstrated.", evidence);
    }

    private static async Task<List<double>> SampleAsync(HttpClient client, Uri baseAddress, Func<string> secretFactory, CancellationToken cancellationToken)
    {
        var samples = new List<double>(SampleCount);
        var stopwatch = new Stopwatch();

        for (var sample = 0; sample < SampleCount; sample++)
        {
            stopwatch.Restart();
            await AccessTokenSupport.CallAccessTokenProbeEndpointAsync(client, baseAddress, secretFactory(), cancellationToken);
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return samples;
    }

    private static (double Median, double InterquartileRange) MedianAndInterquartileRange(List<double> samples)
    {
        var sorted = samples.Order().ToList();

        return (Percentile(sorted, 0.50), Percentile(sorted, 0.75) - Percentile(sorted, 0.25));
    }

    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        var rank = percentile * (sorted.Count - 1);
        var lowerIndex = (int)Math.Floor(rank);
        var upperIndex = (int)Math.Ceiling(rank);

        if (lowerIndex == upperIndex)
            return sorted[lowerIndex];

        var fraction = rank - lowerIndex;
        return sorted[lowerIndex] + (sorted[upperIndex] - sorted[lowerIndex]) * fraction;
    }
}
