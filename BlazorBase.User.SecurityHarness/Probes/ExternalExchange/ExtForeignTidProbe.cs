using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.IdentityProvider;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>
/// A correctly signed token from the legitimate authority, but whose <c>tid</c> claim names a real
/// tenant that the allow-list simply does not contain.
/// </summary>
internal sealed class ExtForeignTidProbe : IAuthProbe
{
    public string Id => "EXT-FOREIGN-TID";
    public string Category => "External token exchange";
    public string Attack => $"Correct signature, legitimate issuer, but tid = '{FakeIdentityProviderTenants.DisallowedTenantId}' - a real tenant the allow-list does not contain.";
    public string Expectation => "Rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();
        await ExternalExchangeSupport.EnsureResolvableSubjectAsync(host.Context);

        var providerHost = ExternalExchangeSupport.GetProviderHost(host.Context);
        var description = ExternalExchangeSupport.CreateValidDescription(providerHost, tidClaim: FakeIdentityProviderTenants.DisallowedTenantId);
        var token = ExternalExchangeSupport.SignWithKeyA(providerHost, description);

        var exchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, token, cancellationToken);
        var evidence = ExternalExchangeSupport.BuildEvidence($"tid = '{FakeIdentityProviderTenants.DisallowedTenantId}', a real but non-allow-listed tenant.", token, exchange);

        if (exchange.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Expected 401, got {(int)exchange.StatusCode} {exchange.StatusCode}. A token for a non-allow-listed tenant was not rejected.", evidence);

        return ProbeResult.Passed("A token for a real but non-allow-listed tenant was rejected.", evidence);
    }
}
