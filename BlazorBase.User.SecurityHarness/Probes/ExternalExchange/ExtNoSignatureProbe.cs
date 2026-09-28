using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>A token with a header and a payload segment but no third (signature) segment at all - not even an empty one.</summary>
internal sealed class ExtNoSignatureProbe : IAuthProbe
{
    public string Id => "EXT-NO-SIGNATURE";
    public string Category => "External token exchange";
    public string Attack => "Header and payload only - the string has no second dot and no signature segment at all.";
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
        var validToken = ExternalExchangeSupport.SignWithKeyA(providerHost, description);
        var token = validToken[..validToken.LastIndexOf('.')];

        var exchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, token, cancellationToken);
        var evidence = ExternalExchangeSupport.BuildEvidence("Header.payload only - no signature segment, not even an empty one.", token, exchange);

        if (exchange.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Expected 401, got {(int)exchange.StatusCode} {exchange.StatusCode}. A two-segment token with no signature at all was not rejected.", evidence);

        return ProbeResult.Passed("A two-segment token with no signature segment at all was rejected.", evidence);
    }
}
