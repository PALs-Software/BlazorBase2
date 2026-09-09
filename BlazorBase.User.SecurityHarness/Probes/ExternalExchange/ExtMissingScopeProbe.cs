using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>
/// Against a host configured to require a scope, a token carrying neither <c>scp</c> nor <c>roles</c>
/// at all. A token that does carry the required scope is exchanged first, as a control, to confirm
/// the isolated host's scope requirement is actually in effect before the missing-scope case is
/// judged against it.
/// </summary>
internal sealed class ExtMissingScopeProbe : IAuthProbe
{
    private const string RequiredScope = "harness.read";

    public string Id => "EXT-MISSING-SCOPE";
    public string Category => "External token exchange";
    public string Attack => $"Against a host requiring the '{RequiredScope}' scope, a token with neither scp nor roles at all.";
    public string Expectation => "Rejected - a control token carrying the required scope is exchanged first to confirm the requirement is actually enforced by this host.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var overrides = new Dictionary<string, string?> { ["ExternalAuthSettings:RequiredScopes:0"] = RequiredScope };
        await using var host = await context.StartIsolatedHostAsync(overrides, cancellationToken);
        using var client = host.Context.HttpClientFactory();
        await ExternalExchangeSupport.EnsureResolvableSubjectAsync(host.Context);

        var providerHost = ExternalExchangeSupport.GetProviderHost(host.Context);

        var controlDescription = ExternalExchangeSupport.CreateValidDescription(providerHost, scopes: RequiredScope);
        var controlToken = ExternalExchangeSupport.SignWithKeyA(providerHost, controlDescription);
        var controlExchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, controlToken, cancellationToken);
        var controlEvidence = ExternalExchangeSupport.BuildEvidence($"Control: scp = '{RequiredScope}', the required scope.", controlToken, controlExchange);

        if (controlExchange.StatusCode != System.Net.HttpStatusCode.OK)
            return ProbeResult.Inconclusive($"The control token carrying the required scope did not get 200 (got {(int)controlExchange.StatusCode}) - the missing-scope case cannot be judged against a scope requirement that is not demonstrably enforced.", controlEvidence);

        var missingDescription = ExternalExchangeSupport.CreateValidDescription(providerHost);
        var missingToken = ExternalExchangeSupport.SignWithKeyA(providerHost, missingDescription);
        var missingExchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, missingToken, cancellationToken);
        var missingEvidence = ExternalExchangeSupport.BuildEvidence("Neither scp nor roles at all.", missingToken, missingExchange);

        if (missingExchange.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Expected 401, got {(int)missingExchange.StatusCode} {missingExchange.StatusCode}. A token with neither scp nor roles was accepted despite a configured scope requirement.", controlEvidence, missingEvidence);

        return ProbeResult.Passed($"A token carrying the required scope was accepted, and a token carrying neither scp nor roles was rejected.", controlEvidence, missingEvidence);
    }
}
