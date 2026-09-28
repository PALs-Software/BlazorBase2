using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>A correctly signed token whose <c>aud</c> claim names an application this host does not own.</summary>
internal sealed class ExtWrongAudienceProbe : IAuthProbe
{
    public string Id => "EXT-WRONG-AUDIENCE";
    public string Category => "External token exchange";
    public string Attack => "Correct signature, but aud names an application other than the one this host is configured to accept.";
    public string Expectation => "Rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();
        await ExternalExchangeSupport.EnsureResolvableSubjectAsync(host.Context);

        var providerHost = ExternalExchangeSupport.GetProviderHost(host.Context);
        var description = ExternalExchangeSupport.CreateValidDescription(providerHost, audience: "some-other-application");
        var token = ExternalExchangeSupport.SignWithKeyA(providerHost, description);

        var exchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, token, cancellationToken);
        var evidence = ExternalExchangeSupport.BuildEvidence("aud = 'some-other-application', not this host's configured audience.", token, exchange);

        if (exchange.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Expected 401, got {(int)exchange.StatusCode} {exchange.StatusCode}. A token for a different audience was not rejected.", evidence);

        return ProbeResult.Passed("A token whose audience does not name this host was rejected.", evidence);
    }
}
