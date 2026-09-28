using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.RedirectFlow;

/// <summary>Every <c>/api/auth/*</c> response - including the redirect flow's - must never be cacheable.</summary>
internal sealed class RedirectNoStoreProbe : IAuthProbe
{
    public string Id => "REDIR-NO-STORE";
    public string Category => "Redirect flow";
    public string Attack => "Inspects the Cache-Control header on api/auth/external/challenge's response.";
    public string Expectation => "Cache-Control: no-store.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Low;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = RedirectFlowSupport.CreateClient(context);

        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Get, context.BaseAddress, "api/auth/external/challenge", cancellationToken: cancellationToken);
        var evidence = exchange.ToEvidence("GET api/auth/external/challenge.");

        var noStore = exchange.ResponseHeaders.CacheControl?.NoStore ?? false;

        if (!noStore)
            return ProbeResult.Failed($"Expected Cache-Control: no-store; got '{exchange.ResponseHeaders.CacheControl}'.", evidence);

        return ProbeResult.Passed("The redirect flow's response carries Cache-Control: no-store.", evidence);
    }
}
