using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.RateLimiting;

/// <summary>
/// Sends more requests than the configured permit limit inside one window against a single account.
/// Runs on its own isolated host with a lowered permit count, so it neither depends on nor pollutes
/// any other probe's rate-limit budget.
/// </summary>
internal sealed class RateLoginTriggersProbe : IAuthProbe
{
    private const int PermitLimit = 3;

    public string Id => "RATE-LOGIN-TRIGGERS";
    public string Category => "Rate limiting";
    public string Attack => $"{PermitLimit + 2} login requests inside one window, against a host configured with a {PermitLimit}-request permit limit.";
    public string Expectation => "The excess requests are rejected with 429.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
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
            var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/login",
                new LoginRequest { Email = CoreSessionSupport.UniqueEmail("rate-trigger"), Password = "definitely-wrong-1A" }, cancellationToken: cancellationToken);
            statusCodes.Add(exchange.StatusCode);
            evidence.Add(exchange.ToEvidence($"Request {request} of {PermitLimit + 2}."));
        }

        var rejectedCount = statusCodes.Count(status => status == System.Net.HttpStatusCode.TooManyRequests);
        var expectedRejections = statusCodes.Count - PermitLimit;

        if (rejectedCount < expectedRejections)
            return ProbeResult.Failed($"Expected at least {expectedRejections} of {statusCodes.Count} requests to be 429; got {rejectedCount}.", evidence);

        return ProbeResult.Passed($"{rejectedCount} of {statusCodes.Count} requests were rejected with 429 once the {PermitLimit}-request permit limit was exceeded.", evidence);
    }
}
