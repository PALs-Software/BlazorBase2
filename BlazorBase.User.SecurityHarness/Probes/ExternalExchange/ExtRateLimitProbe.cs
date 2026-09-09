using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>Floods the exchange endpoint past a lowered permit limit on its own isolated host, so no other probe's budget is affected.</summary>
internal sealed class ExtRateLimitProbe : IAuthProbe
{
    private const int PermitLimit = 3;

    public string Id => "EXT-RATE-LIMIT";
    public string Category => "External token exchange";
    public string Attack => $"{PermitLimit + 2} exchange requests inside one window, against a host configured with a {PermitLimit}-request permit limit.";
    public string Expectation => "The excess requests are rejected with 429 once the limit is reached.";
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
        using var client = host.Context.HttpClientFactory();
        await ExternalExchangeSupport.EnsureResolvableSubjectAsync(host.Context);

        var providerHost = ExternalExchangeSupport.GetProviderHost(host.Context);
        var description = ExternalExchangeSupport.CreateValidDescription(providerHost);
        var token = ExternalExchangeSupport.SignWithKeyA(providerHost, description);

        var evidence = new List<ProbeEvidence>();
        var statusCodes = new List<System.Net.HttpStatusCode>();

        for (var request = 1; request <= PermitLimit + 2; request++)
        {
            var exchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, token, cancellationToken);
            statusCodes.Add(exchange.StatusCode);
            evidence.Add(ExternalExchangeSupport.BuildEvidence($"Request {request} of {PermitLimit + 2}, same valid token.", token, exchange));
        }

        var rejectedCount = statusCodes.Count(status => status == System.Net.HttpStatusCode.TooManyRequests);
        var expectedRejections = statusCodes.Count - PermitLimit;

        if (rejectedCount < expectedRejections)
            return ProbeResult.Failed($"Expected at least {expectedRejections} of {statusCodes.Count} requests to be 429; got {rejectedCount}.", evidence);

        return ProbeResult.Passed($"{rejectedCount} of {statusCodes.Count} requests were rejected with 429 once the {PermitLimit}-request permit limit was exceeded.", evidence);
    }
}
