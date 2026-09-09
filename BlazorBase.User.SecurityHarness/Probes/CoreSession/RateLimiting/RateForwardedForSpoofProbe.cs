using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.RateLimiting;

/// <summary>
/// Repeats <see cref="RateLoginTriggersProbe"/>'s flood, but with a different <c>X-Forwarded-For</c>
/// value on every request - a header this harness never configures <c>UseForwardedHeaders()</c> to
/// trust, so it must have no effect on the partition key.
/// </summary>
internal sealed class RateForwardedForSpoofProbe : IAuthProbe
{
    private const int PermitLimit = 3;

    public string Id => "RATE-FORWARDED-FOR-SPOOF";
    public string Category => "Rate limiting";
    public string Attack => $"The same flood as RATE-LOGIN-TRIGGERS, but each of the {PermitLimit + 2} requests carries a distinct X-Forwarded-For value.";
    public string Expectation => "Still rejected past the permit limit - the limiter must not partition on a client-controlled header the host never configured as trusted.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var overrides = new Dictionary<string, string?>
        {
            ["AuthTokens:RateLimit:PermitLimit"] = PermitLimit.ToString(),
            ["AuthTokens:RateLimit:WindowSeconds"] = "60",
        };
        await using var host = await context.StartIsolatedHostAsync(overrides, cancellationToken);
        using var client = host.Context.HttpClientFactory();

        var evidence = new List<ProbeEvidence>();
        var statusCodes = new List<System.Net.HttpStatusCode>();

        for (var request = 1; request <= PermitLimit + 2; request++)
        {
            var spoofedAddress = $"203.0.113.{request}";
            var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/login",
                new LoginRequest { Email = CoreSessionSupport.UniqueEmail("rate-spoof"), Password = "definitely-wrong-1A" },
                headers: [new("X-Forwarded-For", spoofedAddress)], cancellationToken: cancellationToken);
            statusCodes.Add(exchange.StatusCode);
            evidence.Add(exchange.ToEvidence($"Request {request} of {PermitLimit + 2}, X-Forwarded-For: {spoofedAddress}."));
        }

        var rejectedCount = statusCodes.Count(status => status == System.Net.HttpStatusCode.TooManyRequests);
        var expectedRejections = statusCodes.Count - PermitLimit;

        if (rejectedCount < expectedRejections)
            return ProbeResult.Failed($"Expected at least {expectedRejections} of {statusCodes.Count} requests to be 429 despite the spoofed header; got {rejectedCount}. A distinct X-Forwarded-For value bought each request a fresh partition.", evidence);

        return ProbeResult.Passed($"{rejectedCount} of {statusCodes.Count} requests were still rejected with 429 despite a distinct X-Forwarded-For on every request - the limiter partitions on the socket address, not the header.", evidence);
    }
}
