using System.Net;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.AccessTokens;

/// <summary>Canary for the whole category: a freshly created access token must authenticate.</summary>
internal sealed class AccessTokenValidProbe : IAuthProbe
{
    public string Id => "AT-VALID";
    public string Category => "Access tokens";
    public string Attack => "Creates an access token through IAccessTokenService and presents it to a protected endpoint.";
    public string Expectation => "Accepted. Canary for every other access-token probe.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.AccessTokens;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;

        var created = await AccessTokenSupport.CreateTokenAsync(context, "at-valid", null, null, cancellationToken);
        using var client = context.HttpClientFactory();
        var exchange = await AccessTokenSupport.CallAccessTokenProbeEndpointAsync(client, context.BaseAddress, created.Secret, cancellationToken);
        var evidence = exchange.ToEvidence("GET harness/access-token-probe with a freshly created access token.");

        if (exchange.StatusCode != HttpStatusCode.OK)
            return ProbeResult.Failed($"Expected 200, got {(int)exchange.StatusCode} {exchange.StatusCode}. Every other access-token probe's result is meaningless until this is fixed.", evidence);

        return ProbeResult.Passed("A freshly created access token authenticated successfully.", evidence);
    }
}
