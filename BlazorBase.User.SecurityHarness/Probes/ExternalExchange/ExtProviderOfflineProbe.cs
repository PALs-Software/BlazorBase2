using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>
/// Takes the provider offline after a token has already validated once (and therefore its signing
/// key is already cached), then presents a second, freshly minted token signed with that same cached
/// key while the provider is down. A second token - not the first one again - is deliberate: the
/// endpoint's replay protection would otherwise reject the first token's second presentation on its
/// own terms, independently of whether the provider being offline has anything to do with it.
/// Restores the provider to online afterward, since it is shared with every other probe against
/// this profile.
/// </summary>
internal sealed class ExtProviderOfflineProbe : IAuthProbe
{
    public string Id => "EXT-PROVIDER-OFFLINE";
    public string Category => "External token exchange";
    public string Attack => "Takes the fake provider offline after a token has already validated once, then presents a second, still-unexpired token signed with the same cached key.";
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

            var primingToken = ExternalExchangeSupport.SignWithKeyA(providerHost, ExternalExchangeSupport.CreateValidDescription(providerHost));

            var beforeOffline = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, primingToken, cancellationToken);
            var evidenceBefore = ExternalExchangeSupport.BuildEvidence("While the provider is online - primes the configuration and key cache.", primingToken, beforeOffline);

            if (beforeOffline.StatusCode != System.Net.HttpStatusCode.OK)
                return ProbeResult.Inconclusive($"The priming request did not get 200 (got {(int)beforeOffline.StatusCode}); whether cached keys survive an outage cannot be measured.", evidenceBefore);

            providerHost.GoOffline();

            var secondDescription = ExternalExchangeSupport.CreateValidDescription(providerHost, extraClaims: new Dictionary<string, object> { ["nonce"] = Guid.NewGuid().ToString() });
            var secondToken = ExternalExchangeSupport.SignWithKeyA(providerHost, secondDescription);

            var afterOffline = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, secondToken, cancellationToken);
            var evidenceAfter = ExternalExchangeSupport.BuildEvidence("A second token, signed with the same cached key, presented while the provider is offline.", secondToken, afterOffline);

            if (afterOffline.StatusCode == System.Net.HttpStatusCode.OK)
                return ProbeResult.Passed("The already-cached signing key kept validation working while the provider was offline - no re-fetch was needed for a token signed with a key that already validated once.", evidenceBefore, evidenceAfter);

            return ProbeResult.Failed($"A token signed with an already-cached key stopped validating ({(int)afterOffline.StatusCode} {afterOffline.StatusCode}) once the provider went offline, despite no key rotation - the configuration manager appears not to be caching as expected.", evidenceBefore, evidenceAfter);
        }
        finally
        {
            providerHost.GoOnline();
        }
    }
}

