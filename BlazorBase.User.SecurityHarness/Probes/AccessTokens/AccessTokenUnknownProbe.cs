using System.Net;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.AccessTokens;

/// <summary>A well-formed secret carrying the configured prefix, but never actually issued, must be rejected.</summary>
internal sealed class AccessTokenUnknownProbe : IAuthProbe
{
    public string Id => "AT-UNKNOWN";
    public string Category => "Access tokens";
    public string Attack => "Presents a well-formed but never-issued access-token secret.";
    public string Expectation => "Rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.AccessTokens;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;

        var forgedSecret = "bbat_" + Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        using var client = context.HttpClientFactory();
        var exchange = await AccessTokenSupport.CallAccessTokenProbeEndpointAsync(client, context.BaseAddress, forgedSecret, cancellationToken);
        var evidence = exchange.ToEvidence("GET harness/access-token-probe with a well-formed, never-issued secret.");

        if (exchange.StatusCode != HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Expected 401, got {(int)exchange.StatusCode} {exchange.StatusCode}.", evidence);

        return ProbeResult.Passed("An unknown access token was rejected.", evidence);
    }
}
