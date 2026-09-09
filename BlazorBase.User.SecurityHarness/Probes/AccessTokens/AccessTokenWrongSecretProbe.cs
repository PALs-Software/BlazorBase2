using System.Net;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.AccessTokens;

/// <summary>The right prefix with a wrong secret body must be rejected.</summary>
internal sealed class AccessTokenWrongSecretProbe : IAuthProbe
{
    public string Id => "AT-WRONG-SECRET";
    public string Category => "Access tokens";
    public string Attack => "Presents a valid token's prefix with a different, wrong secret body.";
    public string Expectation => "Rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.AccessTokens;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;

        var created = await AccessTokenSupport.CreateTokenAsync(context, "at-wrong-secret", null, null, cancellationToken);
        var wrongSecret = created.Secret[..^4] + (created.Secret[^4] == 'A' ? 'B' : 'A') + created.Secret[^3..];

        using var client = context.HttpClientFactory();
        var exchange = await AccessTokenSupport.CallAccessTokenProbeEndpointAsync(client, context.BaseAddress, wrongSecret, cancellationToken);
        var evidence = exchange.ToEvidence("GET harness/access-token-probe with the right prefix and a wrong secret.");

        if (exchange.StatusCode != HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Expected 401, got {(int)exchange.StatusCode} {exchange.StatusCode}.", evidence);

        return ProbeResult.Passed("A wrong secret against a real prefix was rejected.", evidence);
    }
}
