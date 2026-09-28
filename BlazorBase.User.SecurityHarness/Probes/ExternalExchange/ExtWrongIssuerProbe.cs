using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>A correctly signed token whose <c>iss</c> claim names an authority this host never trusted.</summary>
internal sealed class ExtWrongIssuerProbe : IAuthProbe
{
    private const string ForeignIssuer = "https://evil.example.invalid/00000000-0000-0000-0000-000000000000/v2.0";

    public string Id => "EXT-WRONG-ISSUER";
    public string Category => "External token exchange";
    public string Attack => $"Correct signature, but iss is a foreign authority ('{ForeignIssuer}') rather than this host's configured authority.";
    public string Expectation => "Rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();
        await ExternalExchangeSupport.EnsureResolvableSubjectAsync(host.Context);

        var providerHost = ExternalExchangeSupport.GetProviderHost(host.Context);
        var description = ExternalExchangeSupport.CreateValidDescription(providerHost, issuer: ForeignIssuer);
        var token = ExternalExchangeSupport.SignWithKeyA(providerHost, description);

        var exchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, token, cancellationToken);
        var evidence = ExternalExchangeSupport.BuildEvidence($"iss = '{ForeignIssuer}', not this host's configured authority.", token, exchange);

        if (exchange.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Expected 401, got {(int)exchange.StatusCode} {exchange.StatusCode}. A token from a foreign issuer was not rejected.", evidence);

        return ProbeResult.Passed("A token claiming a foreign issuer was rejected.", evidence);
    }
}
