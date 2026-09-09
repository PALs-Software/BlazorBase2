using System.Net;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.AccessTokens;

/// <summary>An access token past its own configured expiry must be rejected.</summary>
internal sealed class AccessTokenExpiredProbe : IAuthProbe
{
    public string Id => "AT-EXPIRED";
    public string Category => "Access tokens";
    public string Attack => "Creates an access token with an ExpiresAt already in the past, then presents it.";
    public string Expectation => "Rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.AccessTokens;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;

        var created = await AccessTokenSupport.CreateTokenAsync(context, "at-expired", DateTime.UtcNow.AddMinutes(-5), null, cancellationToken);
        using var client = context.HttpClientFactory();
        var exchange = await AccessTokenSupport.CallAccessTokenProbeEndpointAsync(client, context.BaseAddress, created.Secret, cancellationToken);
        var evidence = exchange.ToEvidence("GET harness/access-token-probe with a secret expired 5 minutes ago.");

        if (exchange.StatusCode != HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Expected 401, got {(int)exchange.StatusCode} {exchange.StatusCode}.", evidence);

        return ProbeResult.Passed("An expired access token was rejected.", evidence);
    }
}
