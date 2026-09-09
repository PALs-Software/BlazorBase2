using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>
/// Takes the provider offline after a token has already validated once (and therefore its signing
/// key is already cached), then presents a previously working token again while the provider is
/// down. Restores the provider to online afterward, since it is shared with every other probe
/// against this profile.
/// </summary>
internal sealed class ExtProviderOfflineProbe : IAuthProbe
{
    public string Id => "EXT-PROVIDER-OFFLINE";
    public string Category => "External token exchange";
    public string Attack => "Takes the fake provider offline after a token has already validated once, then presents a previously working, still-unexpired token again.";
    public string Expectation => "Record whether the cached signing key keeps validation working while the provider is unreachable.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Low;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var providerHost = ExternalExchangeSupport.GetProviderHost(context);

        try
        {
            await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
            using var client = host.Context.HttpClientFactory();
            await ExternalExchangeSupport.EnsureResolvableSubjectAsync(host.Context);

            var description = ExternalExchangeSupport.CreateValidDescription(providerHost);
            var token = ExternalExchangeSupport.SignWithKeyA(providerHost, description);

            var beforeOffline = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, token, cancellationToken);
            var evidenceBefore = ExternalExchangeSupport.BuildEvidence("While the provider is online - primes the configuration and key cache.", token, beforeOffline);

            if (beforeOffline.StatusCode != System.Net.HttpStatusCode.OK)
                return ProbeResult.Inconclusive($"The priming request did not get 200 (got {(int)beforeOffline.StatusCode}); whether cached keys survive an outage cannot be measured.", evidenceBefore);

            providerHost.GoOffline();

            var afterOffline = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, token, cancellationToken);
            var evidenceAfter = ExternalExchangeSupport.BuildEvidence("The same token, presented again while the provider is offline.", token, afterOffline);

            if (afterOffline.StatusCode == System.Net.HttpStatusCode.OK)
                return ProbeResult.Passed("The already-cached signing key kept validation working while the provider was offline - no re-fetch was needed for a token that already validated once.", evidenceBefore, evidenceAfter);

            return ProbeResult.Failed($"A token that validated while the provider was online stopped validating ({(int)afterOffline.StatusCode} {afterOffline.StatusCode}) once the provider went offline, despite no key rotation - the configuration manager appears not to be caching as expected.", evidenceBefore, evidenceAfter);
        }
        finally
        {
            providerHost.GoOnline();
        }
    }
}
