using System.Net;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.RedirectFlow;

/// <summary>Floods api/auth/external/challenge past a lowered permit limit, on its own isolated host.</summary>
internal sealed class RedirectChallengeRateLimitProbe : IAuthProbe
{
    private const int PermitLimit = 3;

    public string Id => "REDIR-CHALLENGE-RATE-LIMIT";
    public string Category => "Redirect flow";
    public string Attack => $"{PermitLimit + 2} requests to api/auth/external/challenge inside one window, against a host configured with a {PermitLimit}-request permit limit.";
    public string Expectation => "The excess requests are rejected with 429.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var overrides = new Dictionary<string, string?>
        {
            ["AuthTokens:RateLimit:PermitLimit"] = PermitLimit.ToString(),
            ["AuthTokens:RateLimit:WindowSeconds"] = "60",
        };
        await using var host = await context.StartIsolatedHostAsync(overrides, cancellationToken);
        using var client = RedirectFlowSupport.CreateClient(host.Context);

        var evidence = new List<ProbeEvidence>();
        var statusCodes = new List<HttpStatusCode>();

        for (var request = 1; request <= PermitLimit + 2; request++)
        {
            var hop = await RedirectFlowSupport.ChallengeOnceAsync(client, host.Context.BaseAddress, cancellationToken);
            statusCodes.Add(hop.Exchange.StatusCode);
            evidence.Add(hop.Exchange.ToEvidence($"Challenge request {request} of {PermitLimit + 2}."));
        }

        var rejectedCount = statusCodes.Count(status => status == HttpStatusCode.TooManyRequests);
        var expectedRejections = statusCodes.Count - PermitLimit;

        if (rejectedCount < expectedRejections)
            return ProbeResult.Failed($"Expected at least {expectedRejections} of {statusCodes.Count} requests to be 429; got {rejectedCount}.", evidence);

        return ProbeResult.Passed($"{rejectedCount} of {statusCodes.Count} challenge requests were rejected with 429 once the {PermitLimit}-request permit limit was exceeded.", evidence);
    }
}
