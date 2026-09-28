using System.Net;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.RedirectFlow;

/// <summary>Calling <c>complete</c> with no prior <c>challenge</c> - no external sign-in to read - must not mint a session.</summary>
internal sealed class RedirectCompleteWithoutChallengeProbe : IAuthProbe
{
    public string Id => "REDIR-COMPLETE-WITHOUT-CHALLENGE";
    public string Category => "Redirect flow";
    public string Attack => "GET api/auth/external/complete with no prior challenge.";
    public string Expectation => "Rejected; no session is minted.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        await RedirectFlowSupport.EnsureResolvableSubjectAsync(context);
        using var client = RedirectFlowSupport.CreateClient(context);

        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Get, context.BaseAddress, "api/auth/external/complete?returnUrl=/dashboard", cancellationToken: cancellationToken);
        var evidence = exchange.ToEvidence("GET api/auth/external/complete with no prior challenge and no cookies attached.");

        if (exchange.StatusCode != HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Expected 401, got {(int)exchange.StatusCode} {exchange.StatusCode}.", evidence);

        return ProbeResult.Passed("Completing without a prior challenge was rejected with 401.", evidence);
    }
}
