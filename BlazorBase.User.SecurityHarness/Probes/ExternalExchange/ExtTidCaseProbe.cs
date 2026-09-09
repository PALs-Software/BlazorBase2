using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.IdentityProvider;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>
/// The allowed tenant's <c>tid</c>, presented with different casing and with surrounding
/// whitespace, to see whether the allow-list's own comparison can be evaded by either shape - as
/// opposed to <see cref="ExtForeignTidProbe"/>, which presents a genuinely different tenant.
/// </summary>
internal sealed class ExtTidCaseProbe : IAuthProbe
{
    public string Id => "EXT-TID-CASE";
    public string Category => "External token exchange";
    public string Attack => "The allowed tenant's tid, once upper-cased and once padded with surrounding whitespace - the same tenant, not a different one.";
    public string Expectation => "Record the outcome for each shape. Accepting a different-cased tid is the allow-list's own case-insensitive comparison at work, not a bypass; accepting a whitespace-padded tid that does not equal the configured value verbatim would be one.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();
        await ExternalExchangeSupport.EnsureResolvableSubjectAsync(host.Context);

        var providerHost = ExternalExchangeSupport.GetProviderHost(host.Context);

        var upperCasedTid = FakeIdentityProviderTenants.AllowedTenantId.ToUpperInvariant();
        var upperExchange = await ExchangeWithTidAsync(client, host.Context.BaseAddress, providerHost, upperCasedTid, cancellationToken);

        var paddedTid = $" {FakeIdentityProviderTenants.AllowedTenantId} ";
        var paddedExchange = await ExchangeWithTidAsync(client, host.Context.BaseAddress, providerHost, paddedTid, cancellationToken);

        var evidence = new[]
        {
            ExternalExchangeSupport.BuildEvidence($"tid = '{upperCasedTid}' (upper-cased).", upperExchange.Token, upperExchange.Exchange),
            ExternalExchangeSupport.BuildEvidence($"tid = '{paddedTid}' (surrounding whitespace).", paddedExchange.Token, paddedExchange.Exchange),
        };

        var upperAccepted = upperExchange.Exchange.StatusCode == System.Net.HttpStatusCode.OK;
        var paddedAccepted = paddedExchange.Exchange.StatusCode == System.Net.HttpStatusCode.OK;

        if (paddedAccepted)
            return ProbeResult.Failed("A whitespace-padded tid - not equal to the configured value verbatim - was accepted. That is an allow-list bypass, not case-insensitivity.", evidence);

        return ProbeResult.Passed($"Different casing was {(upperAccepted ? "accepted (the allow-list's own case-insensitive comparison, not a bypass of a different tenant)" : "rejected")}. Surrounding whitespace was rejected, as a differing string should be.", evidence);
    }

    private static async Task<(string Token, ProbeExchange Exchange)> ExchangeWithTidAsync(HttpClient client, Uri baseAddress, FakeIdentityProviderHost providerHost, string tidClaim, CancellationToken cancellationToken)
    {
        var description = ExternalExchangeSupport.CreateValidDescription(providerHost, tidClaim: tidClaim);
        var token = ExternalExchangeSupport.SignWithKeyA(providerHost, description);
        var exchange = await ExternalExchangeSupport.ExchangeAsync(client, baseAddress, token, cancellationToken);

        return (token, exchange);
    }
}
