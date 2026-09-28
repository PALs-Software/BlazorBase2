using System.Net;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.RedirectFlow;

/// <summary>A legitimate, local returnUrl carrying a query string and a fragment survives the whole redirect flow unchanged.</summary>
internal sealed class RedirectReturnUrlPreservedProbe : IAuthProbe
{
    private const string ReturnUrl = "/some/page?x=1#y";

    public string Id => "REDIR-RETURNURL-PRESERVED";
    public string Category => "Redirect flow";
    public string Attack => $"Completes the redirect flow with returnUrl='{ReturnUrl}'.";
    public string Expectation => "The final redirect's Location preserves the returnUrl exactly.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        await RedirectFlowSupport.EnsureResolvableSubjectAsync(context);
        using var client = RedirectFlowSupport.CreateClient(context);

        var chain = await RedirectFlowSupport.RunChallengeAsync(client, context.BaseAddress, ReturnUrl, cancellationToken);
        var evidence = chain.ToEvidence($"Redirect flow with returnUrl='{ReturnUrl}'.");

        if (chain.Final.Exchange.StatusCode is not (HttpStatusCode.Redirect or HttpStatusCode.Found))
            return ProbeResult.Inconclusive(
                $"The flow did not complete with a redirect (final hop: {(int)chain.Final.Exchange.StatusCode} {chain.Final.Exchange.StatusCode}) - the preserved-returnUrl outcome could not be observed.", evidence);

        var location = chain.Final.Exchange.ResponseHeaders.TryGetValues("Location", out var values) ? values.FirstOrDefault() : null;

        if (location != ReturnUrl)
            return ProbeResult.Failed($"Expected the final redirect to preserve '{ReturnUrl}' exactly; got '{location}'.", evidence);

        return ProbeResult.Passed($"The final redirect preserved '{ReturnUrl}' exactly.", evidence);
    }
}
