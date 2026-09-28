using System.Net;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.AccessTokens;

/// <summary>A revoked token must be rejected on the very next request, never merely after a cache expiry.</summary>
internal sealed class AccessTokenRevokedProbe : IAuthProbe
{
    public string Id => "AT-REVOKED";
    public string Category => "Access tokens";
    public string Attack => "Revokes a valid access token, then presents it immediately afterward.";
    public string Expectation => "Rejected immediately, not after a cache expiry.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.AccessTokens;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;

        var created = await AccessTokenSupport.CreateTokenAsync(context, "at-revoked", null, null, cancellationToken);
        using var client = context.HttpClientFactory();

        var beforeRevoke = await AccessTokenSupport.CallAccessTokenProbeEndpointAsync(client, context.BaseAddress, created.Secret, cancellationToken);
        var revoked = await AccessTokenSupport.RevokeTokenAsync(context, created.Id, cancellationToken);
        var afterRevoke = await AccessTokenSupport.CallAccessTokenProbeEndpointAsync(client, context.BaseAddress, created.Secret, cancellationToken);

        var evidence = new List<ProbeEvidence>
        {
            beforeRevoke.ToEvidence("Before revocation."),
            new("RevokeAsync result.", Response: revoked.ToString()),
            afterRevoke.ToEvidence("Immediately after revocation, same secret."),
        };

        if (!revoked)
            return ProbeResult.Inconclusive("RevokeAsync reported no matching token; the revocation itself did not happen.", evidence);

        if (afterRevoke.StatusCode != HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Expected 401 immediately after revocation, got {(int)afterRevoke.StatusCode} {afterRevoke.StatusCode}.", evidence);

        return ProbeResult.Passed("A revoked access token was rejected on the very next request.", evidence);
    }
}
