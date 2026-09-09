using System.Net;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.AccessTokens;

/// <summary>
/// A machine token bound to a user account that is later deactivated - whether the token still
/// authenticates is a real question about the access-token subsystem's design, not a harness
/// assumption: <c>AccessTokenService.ValidateAsync</c> re-reads revocation and expiry from the
/// database on every call, but never re-checks the owning user's <c>IsActive</c> flag.
/// </summary>
internal sealed class AccessTokenInactiveOwnerProbe : IAuthProbe
{
    public string Id => "AT-INACTIVE-OWNER";
    public string Category => "Access tokens";
    public string Attack => "Creates an access token bound to a user, deactivates that user, then presents the token again.";
    public string Expectation => "Recorded either way: a machine token that outlives its owner's deactivation is a finding.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.AccessTokens;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;

        var email = CoreSessionSupport.UniqueEmail("at-inactive-owner");
        var owner = await CoreSessionSupport.SeedUserAsync(context, email);
        var created = await AccessTokenSupport.CreateTokenAsync(context, "at-inactive-owner", null, owner.Id, cancellationToken);

        using var client = context.HttpClientFactory();
        var whileActive = await AccessTokenSupport.CallAccessTokenProbeEndpointAsync(client, context.BaseAddress, created.Secret, cancellationToken);

        await AccessTokenSupport.SetUserActiveAsync(context, owner.Id, isActive: false);
        var afterDeactivation = await AccessTokenSupport.CallAccessTokenProbeEndpointAsync(client, context.BaseAddress, created.Secret, cancellationToken);

        var evidence = new List<ProbeEvidence>
        {
            whileActive.ToEvidence("While the owning account is active."),
            afterDeactivation.ToEvidence("Immediately after the owning account was deactivated, same token."),
        };

        if (afterDeactivation.StatusCode == HttpStatusCode.OK)
            return ProbeResult.Failed(
                "The access token still authenticates after its owning account was deactivated: AccessTokenService.ValidateAsync checks IsRevoked and ExpiresAt but never the owner's IsActive flag.",
                evidence);

        return ProbeResult.Passed("The access token stopped authenticating once its owner was deactivated.", evidence);
    }
}
