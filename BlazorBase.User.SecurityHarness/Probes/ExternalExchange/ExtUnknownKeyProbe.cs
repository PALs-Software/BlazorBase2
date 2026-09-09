using System.Security.Cryptography;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.IdentityProvider;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>A valid RS256 signature made with a key the provider never published in its JWKS at all.</summary>
internal sealed class ExtUnknownKeyProbe : IAuthProbe
{
    public string Id => "EXT-UNKNOWN-KEY";
    public string Category => "External token exchange";
    public string Attack => "A correctly formed RS256 token, signed with an RSA key that is not, and never was, in the provider's JWKS.";
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

        using var strangerKey = RSA.Create(2048);
        var token = FakeTokenBuilder.CreateRsaSignedToken(description, "harness-key-stranger", strangerKey);

        var exchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, token, cancellationToken);
        var evidence = ExternalExchangeSupport.BuildEvidence("Signed with a key never published to the JWKS, under a kid the provider has never heard of.", token, exchange);

        if (exchange.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Expected 401, got {(int)exchange.StatusCode} {exchange.StatusCode}. A token signed by an unpublished key was not rejected.", evidence);

        return ProbeResult.Passed("A token signed by a key never published to the JWKS was rejected.", evidence);
    }
}
