using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>
/// The canary the whole battery depends on: a correctly signed, correctly scoped external token
/// exchanged for a local session with no attack at all. Every other probe in this category is only
/// meaningful if this one passes.
/// </summary>
internal sealed class ExtValidProbe : IAuthProbe
{
    public string Id => "EXT-VALID";
    public string Category => "External token exchange";
    public string Attack => "A correctly signed token, the allowed tenant, the configured audience - no attack.";
    public string Expectation => "200 and a local token pair. Every other probe in this category is meaningless if this one fails.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();
        await ExternalExchangeSupport.EnsureResolvableSubjectAsync(host.Context);

        var providerHost = ExternalExchangeSupport.GetProviderHost(host.Context);
        var description = ExternalExchangeSupport.CreateValidDescription(providerHost);
        var token = ExternalExchangeSupport.SignWithKeyA(providerHost, description);

        var exchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, token, cancellationToken);
        var evidence = ExternalExchangeSupport.BuildEvidence("A correctly signed, correctly scoped external token.", token, exchange);

        if (exchange.StatusCode != System.Net.HttpStatusCode.OK)
            return ProbeResult.Failed($"Expected 200, got {(int)exchange.StatusCode} {exchange.StatusCode}. This is the canary - every other EXT-* result is meaningless until this passes.", evidence);

        if (!CoreSessionSupport.TryDeserialize<LoginResponse>(exchange.ResponseBody, out var tokens) || tokens is null || string.IsNullOrEmpty(tokens.AccessToken))
            return ProbeResult.Failed("Got 200 but the response did not carry a usable access token.", evidence);

        return ProbeResult.Passed("A correctly signed, correctly scoped external token exchanged for a local token pair.", evidence);
    }
}
