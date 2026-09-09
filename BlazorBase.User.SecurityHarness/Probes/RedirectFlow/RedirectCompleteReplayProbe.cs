using System.Net;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.RedirectFlow;

/// <summary>Replaying the exact <c>complete</c> callback URL a second time must not mint a second session.</summary>
internal sealed class RedirectCompleteReplayProbe : IAuthProbe
{
    public string Id => "REDIR-COMPLETE-REPLAY";
    public string Category => "Redirect flow";
    public string Attack => "Completes the redirect flow once, then replays the exact same complete URL a second time with the same client.";
    public string Expectation => "The replayed completion is rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        await RedirectFlowSupport.EnsureResolvableSubjectAsync(context);
        using var client = RedirectFlowSupport.CreateClient(context);

        var chain = await RedirectFlowSupport.RunChallengeAsync(client, context.BaseAddress, "/dashboard", cancellationToken);
        var firstCompletion = chain.Final;
        var evidence = new List<ProbeEvidence> { chain.ToEvidence("Initial completion of the redirect flow.") };

        if (firstCompletion.Exchange.StatusCode is not (HttpStatusCode.Redirect or HttpStatusCode.Found))
            return ProbeResult.Inconclusive(
                $"The initial completion did not succeed (final hop: {(int)firstCompletion.Exchange.StatusCode} {firstCompletion.Exchange.StatusCode}) - replay could not be tested. " +
                "A 401 here is ExternalAuthRedirectEndpointMapper.Complete's own httpContext.AuthenticateAsync(externalScheme) call failing structurally: a real RemoteAuthenticationHandler (OpenIdConnect) never succeeds AuthenticateAsync outside its own callback processing, so Complete rejects every genuine completion regardless of whether the OIDC handshake itself succeeded - the redirect flow, wired exactly as ExternalAuthRedirectEndpointMapper's own XML documentation describes, can never mint a session.", evidence);

        var replay = await CoreSessionSupport.SendAsync(client, HttpMethod.Get, firstCompletion.RequestUri, string.Empty, cancellationToken: cancellationToken);
        evidence.Add(replay.ToEvidence("Replaying the same complete URL a second time, with the same client."));

        if (replay.StatusCode != HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Expected the replayed completion to be rejected with 401; got {(int)replay.StatusCode} {replay.StatusCode}.", evidence);

        return ProbeResult.Passed("Replaying the completed callback a second time was rejected with 401.", evidence);
    }
}
