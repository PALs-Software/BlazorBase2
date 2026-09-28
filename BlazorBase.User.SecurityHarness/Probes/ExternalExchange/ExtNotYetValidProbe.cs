using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>A correctly signed token whose <c>nbf</c> is in the future, well outside the clock skew.</summary>
internal sealed class ExtNotYetValidProbe : IAuthProbe
{
    public string Id => "EXT-NOT-YET-VALID";
    public string Category => "External token exchange";
    public string Attack => "Correctly signed, but nbf is 10 minutes in the future.";
    public string Expectation => "Rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();
        await ExternalExchangeSupport.EnsureResolvableSubjectAsync(host.Context);

        var providerHost = ExternalExchangeSupport.GetProviderHost(host.Context);
        var now = DateTimeOffset.UtcNow;
        var description = ExternalExchangeSupport.CreateValidDescription(providerHost, issuedAt: now, notBefore: now.AddMinutes(10), expiresAt: now.AddMinutes(20));
        var token = ExternalExchangeSupport.SignWithKeyA(providerHost, description);

        var exchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, token, cancellationToken);
        var evidence = ExternalExchangeSupport.BuildEvidence("nbf 10 minutes in the future.", token, exchange);

        if (exchange.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Expected 401, got {(int)exchange.StatusCode} {exchange.StatusCode}. A not-yet-valid token was not rejected.", evidence);

        return ProbeResult.Passed("A token not yet valid (nbf in the future) was rejected.", evidence);
    }
}
