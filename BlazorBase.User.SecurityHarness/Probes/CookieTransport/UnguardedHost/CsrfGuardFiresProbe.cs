using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CookieTransport.UnguardedHost;

/// <summary>
/// Targets <see cref="HarnessProfile.CookieWithoutCsrfMiddleware"/>, a host that registers the cookie
/// transport but never calls <c>app.UseBlazorBaseCsrf()</c>. That host never serves a single request
/// successfully - <c>BlazorBaseCsrfStartupGuardFilter</c> throws on the very first one - so this
/// probe reads <see cref="ProbeContext.StartupGuardFailure"/>, the message the harness's own
/// <c>GuardExceptionCapture</c> recorded from that failed request, rather than making any request of
/// its own.
/// </summary>
internal sealed class CsrfGuardFiresProbe : IAuthProbe
{
    public string Id => "CSRF-GUARD-FIRES";
    public string Category => "CSRF";
    public string Attack => "Start the CookieWithoutCsrfMiddleware profile - the cookie transport registered without app.UseBlazorBaseCsrf() - and observe what happens.";
    public string Expectation => "The framework refuses to serve, with a message naming the cause.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.CookieWithoutCsrfMiddleware;

    public Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var failure = context.StartupGuardFailure;
        var evidence = new ProbeEvidence(
            "The startup guard's recorded failure message for this profile.",
            Request: $"GET / against the {HarnessProfile.CookieWithoutCsrfMiddleware} profile, sent once by HarnessHostFactory while starting the host.",
            Response: failure ?? "(no failure was recorded - the host may have started and served successfully)");

        if (string.IsNullOrEmpty(failure))
            return Task.FromResult(ProbeResult.Failed(
                "The CookieWithoutCsrfMiddleware profile served a request without the CSRF middleware in place - no startup guard fired.",
                evidence));

        if (!failure.Contains("UseBlazorBaseCsrf", StringComparison.Ordinal))
            return Task.FromResult(ProbeResult.Failed(
                $"The host failed to start, but the recorded message does not name app.UseBlazorBaseCsrf() as the cause: {failure}",
                evidence));

        return Task.FromResult(ProbeResult.Passed(
            $"The framework refused to serve, naming the exact missing call: {failure}",
            evidence));
    }
}
