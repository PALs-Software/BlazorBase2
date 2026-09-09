using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.IdentityProvider;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>An otherwise-valid token with <c>alg: none</c> and no signature segment at all.</summary>
internal sealed class ExtAlgNoneProbe : IAuthProbe
{
    public string Id => "EXT-ALG-NONE";
    public string Category => "External token exchange";
    public string Attack => "An otherwise-valid token with alg: none and an empty signature segment.";
    public string Expectation => "Rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();
        await ExternalExchangeSupport.EnsureResolvableSubjectAsync(host.Context);

        var providerHost = ExternalExchangeSupport.GetProviderHost(host.Context);
        var description = ExternalExchangeSupport.CreateValidDescription(providerHost);
        var token = FakeTokenBuilder.CreateUnsignedToken(description);

        var exchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, token, cancellationToken);
        var evidence = ExternalExchangeSupport.BuildEvidence("alg: none, empty signature.", token, exchange);

        if (exchange.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Expected 401, got {(int)exchange.StatusCode} {exchange.StatusCode}. An alg: none token was not rejected.", evidence);

        return ProbeResult.Passed("An alg: none token with no signature was rejected.", evidence);
    }
}
