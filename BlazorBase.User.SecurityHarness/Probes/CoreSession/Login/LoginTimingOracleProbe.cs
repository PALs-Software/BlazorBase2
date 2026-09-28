using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.Login;

/// <summary>
/// Samples response latency for a wrong password against a real account and for a login attempt
/// against an account that does not exist, looking for a timing side channel the response body
/// itself does not carry (both cases already return an identical body - see
/// <see cref="LoginUnknownUserProbe"/>). The two groups are interleaved one request at a time (see
/// <see cref="InterleavedTimingSampler"/>) and reduced to a verdict through
/// <see cref="TimingSideChannelAnalysis"/>'s bootstrap confidence interval, so a transient burst of
/// unrelated system load cannot masquerade as a side channel by landing inside only one group's block.
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

        var (knownUserSamples, unknownUserSamples) = await InterleavedTimingSampler.SampleAsync(
            ct => SendAsync(client, host.Context.BaseAddress, knownEmail, ct),
            ct => SendAsync(client, host.Context.BaseAddress, unknownEmail, ct),
            SampleCount, cancellationToken);

        var verdict = TimingSideChannelAnalysis.Evaluate(knownUserSamples, unknownUserSamples);

        var summary =
            $"{SampleCount} samples each, interleaved. Known-user-wrong-password median {verdict.MedianA:F2} ms (IQR {verdict.InterquartileRangeA:F2} ms); " +
            $"unknown-user median {verdict.MedianB:F2} ms (IQR {verdict.InterquartileRangeB:F2} ms); delta {verdict.Delta:F2} ms " +
            $"(99% bootstrap interval [{verdict.LowerBound:F2}, {verdict.UpperBound:F2}] ms).";

        var evidence = new ProbeEvidence(
            "Raw sample latencies, in milliseconds.",
            Request: $"POST api/auth/login x{SampleCount} (known user, wrong password) and x{SampleCount} (unknown user), interleaved one-for-one",
            Response: $"known: [{string.Join(", ", knownUserSamples.Select(sample => sample.ToString("F2")))}]\n" +
                      $"unknown: [{string.Join(", ", unknownUserSamples.Select(sample => sample.ToString("F2")))}]");

        if (verdict.SideChannelDemonstrated)
            return ProbeResult.Failed($"{summary} The interval excludes zero - a timing side channel is measurable.", evidence);

        return ProbeResult.Inconclusive($"{summary} The interval includes zero - no timing side channel demonstrated.", evidence);
    }

    private static Task SendAsync(HttpClient client, Uri baseAddress, string email, CancellationToken cancellationToken) =>
        CoreSessionSupport.SendAsync(client, HttpMethod.Post, baseAddress, "api/auth/login",
            new LoginRequest { Email = email, Password = "definitely-wrong-1A" }, cancellationToken: cancellationToken);
}
