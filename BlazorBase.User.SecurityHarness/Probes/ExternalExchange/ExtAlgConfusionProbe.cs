using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.IdentityProvider;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>
/// The classic algorithm-confusion attack: an <c>HS256</c> token whose HMAC secret is the
/// provider's own RSA public key material, under key A's own <c>kid</c> - tried against several
/// encodings of that key, since a single attempt proves less than several would.
/// </summary>
internal sealed class ExtAlgConfusionProbe : IAuthProbe
{
    public string Id => "EXT-ALG-CONFUSION";
    public string Category => "External token exchange";
    public string Attack => "An HS256 token, kid = key A, HMAC-signed with key A's own RSA public key material - tried as raw modulus bytes, SPKI DER, SPKI PEM text, and the base64 text of each.";
    public string Expectation => "Rejected for every encoding tried.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();
        await ExternalExchangeSupport.EnsureResolvableSubjectAsync(host.Context);

        var providerHost = ExternalExchangeSupport.GetProviderHost(host.Context);
        var description = ExternalExchangeSupport.CreateValidDescription(providerHost);
        var publicKey = providerHost.KeyRegistry.GetKey(FakeSigningKeyRegistry.KeyIdA);
        var secrets = ExternalExchangeSupport.BuildAlgConfusionSecrets(publicKey);

        var evidence = new List<ProbeEvidence>();
        var accepted = new List<string>();

        foreach (var (encodingName, secret) in secrets)
        {
            var token = FakeTokenBuilder.CreateHmacSignedToken(description, FakeSigningKeyRegistry.KeyIdA, secret);
            var exchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, token, cancellationToken);
            evidence.Add(ExternalExchangeSupport.BuildEvidence($"HS256, secret = key A's public key as {encodingName}.", token, exchange));

            if (exchange.StatusCode == System.Net.HttpStatusCode.OK)
                accepted.Add(encodingName);
        }

        if (accepted.Count > 0)
            return ProbeResult.Failed($"Algorithm confusion succeeded with encoding(s): {string.Join(", ", accepted)}. The validator accepted an HS256 token whose secret was the provider's own RSA public key.", evidence);

        return ProbeResult.Passed($"Algorithm confusion was rejected for all {secrets.Count} encoding(s) of key A's public key tried as the HMAC secret.", evidence);
    }
}
