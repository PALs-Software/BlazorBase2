using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.IdentityProvider;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>
/// A token whose header <c>kid</c> names key A, but whose signature was actually made with key B -
/// publishing B alongside A for the duration of the probe, so the failure is specifically the
/// kid/signature mismatch rather than an incidentally unpublished key, and restoring the registry to
/// its starting state (B withheld) afterward so it does not leak into a probe that runs later.
/// </summary>
internal sealed class ExtKidMismatchProbe : IAuthProbe
{
    public string Id => "EXT-KID-MISMATCH";
    public string Category => "External token exchange";
    public string Attack => "Header kid names key A, but the signature was made with key B - both published, so the mismatch itself is the only variable.";
    public string Expectation => "Rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();
        await ExternalExchangeSupport.EnsureResolvableSubjectAsync(host.Context);

        var providerHost = ExternalExchangeSupport.GetProviderHost(host.Context);
        providerHost.KeyRegistry.Publish(FakeSigningKeyRegistry.KeyIdB);

        try
        {
            var description = ExternalExchangeSupport.CreateValidDescription(providerHost);
            var token = FakeTokenBuilder.CreateRsaSignedToken(description, FakeSigningKeyRegistry.KeyIdA, providerHost.KeyRegistry.GetKey(FakeSigningKeyRegistry.KeyIdB));

            var exchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, token, cancellationToken);
            var evidence = ExternalExchangeSupport.BuildEvidence("kid names key A, signed with key B - both published.", token, exchange);

            if (exchange.StatusCode != System.Net.HttpStatusCode.Unauthorized)
                return ProbeResult.Failed($"Expected 401, got {(int)exchange.StatusCode} {exchange.StatusCode}. A kid/signature mismatch was not rejected.", evidence);

            return ProbeResult.Passed("A token naming key A's kid but signed with key B was rejected.", evidence);
        }
        finally
        {
            providerHost.KeyRegistry.Withhold(FakeSigningKeyRegistry.KeyIdB);
        }
    }
}
