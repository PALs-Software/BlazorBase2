using System.Diagnostics;
using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.Login;

/// <summary>
/// Samples response latency for a wrong password against a real account and for a login attempt
/// against an account that does not exist, looking for a timing side channel the response body
/// itself does not carry (both cases already return an identical body - see
/// <see cref="LoginUnknownUserProbe"/>). Reports the median and interquartile range of both
/// distributions and their delta either way; only calls it a finding when the delta clearly exceeds
/// the noise in both distributions, and says "inconclusive" rather than guessing otherwise.
/// </summary>
internal sealed class LoginTimingOracleProbe : IAuthProbe
{
    private const int SampleCount = 30;

    /// <summary>
    /// A permit limit far above the sample count, because this probe measures the cost of the
    /// password hash. With the real limit the rate limiter would short-circuit most samples and the
    /// measurement would describe the limiter instead of the login path.
    /// </summary>
    private static readonly Dictionary<string, string?> MeasurementOverrides =
        new() { ["AuthTokens:RateLimit:PermitLimit"] = "1000" };

    public string Id => "LOGIN-TIMING-ORACLE";
    public string Category => "Login";
    public string Attack => $"{SampleCount} timed wrong-password attempts against a real account, and {SampleCount} against an account that does not exist.";
    public string Expectation => "The two response-time distributions overlap - no timing side channel distinguishes a real account from a fictitious one.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(MeasurementOverrides, cancellationToken);
        var knownEmail = CoreSessionSupport.UniqueEmail("timing-known");
        await CoreSessionSupport.SeedUserAsync(host.Context, knownEmail);
        var unknownEmail = CoreSessionSupport.UniqueEmail("timing-unknown");

        using var client = host.Context.HttpClientFactory();

        var knownUserSamples = await SampleAsync(client, host.Context.BaseAddress, knownEmail, cancellationToken);
        var unknownUserSamples = await SampleAsync(client, host.Context.BaseAddress, unknownEmail, cancellationToken);

        var (knownMedian, knownIqr) = MedianAndInterquartileRange(knownUserSamples);
        var (unknownMedian, unknownIqr) = MedianAndInterquartileRange(unknownUserSamples);
        var delta = Math.Abs(knownMedian - unknownMedian);
        var noiseFloor = Math.Max(knownIqr, unknownIqr);

        var summary =
            $"{SampleCount} samples each. Known-user-wrong-password median {knownMedian:F2} ms (IQR {knownIqr:F2} ms); " +
            $"unknown-user median {unknownMedian:F2} ms (IQR {unknownIqr:F2} ms); delta {delta:F2} ms.";

        var evidence = new ProbeEvidence(
            "Raw sample latencies, in milliseconds.",
            Request: $"POST api/auth/login x{SampleCount} (known user, wrong password) and x{SampleCount} (unknown user)",
            Response: $"known: [{string.Join(", ", knownUserSamples.Select(sample => sample.ToString("F2")))}]\n" +
                      $"unknown: [{string.Join(", ", unknownUserSamples.Select(sample => sample.ToString("F2")))}]");

        if (delta > noiseFloor)
            return ProbeResult.Failed($"{summary} The delta exceeds the noise floor (max IQR {noiseFloor:F2} ms) - a timing side channel is measurable.", evidence);

        return ProbeResult.Inconclusive($"{summary} The delta sits inside the noise (max IQR {noiseFloor:F2} ms) - no timing side channel demonstrated.", evidence);
    }

    private static async Task<List<double>> SampleAsync(HttpClient client, Uri baseAddress, string email, CancellationToken cancellationToken)
    {
        var samples = new List<double>(SampleCount);
        var stopwatch = new Stopwatch();

        for (var sample = 0; sample < SampleCount; sample++)
        {
            stopwatch.Restart();
            await CoreSessionSupport.SendAsync(client, HttpMethod.Post, baseAddress, "api/auth/login",
                new LoginRequest { Email = email, Password = "definitely-wrong-1A" }, cancellationToken: cancellationToken);
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return samples;
    }

    /// <summary>Median and interquartile range (P75 - P25) using linear interpolation between ranks.</summary>
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
