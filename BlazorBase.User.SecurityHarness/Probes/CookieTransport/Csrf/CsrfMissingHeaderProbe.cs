using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.CookieTransport.Csrf;

/// <summary>A state-changing request with the live session cookie but no antiforgery header at all.</summary>
internal sealed class CsrfMissingHeaderProbe : IAuthProbe
{
    public string Id => "CSRF-MISSING-HEADER";
    public string Category => "CSRF";
    public string Attack => "POST a state-changing request with the session cookie attached but no antiforgery header.";
    public string Expectation => "Rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Cookie;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();
        var session = await CookieTransportSupport.EstablishSessionAsync(context, client, "csrf-missing-header", cancellationToken);

        if (session.Tokens is null)
            return ProbeResult.Inconclusive("Login over the cookie transport did not answer 200.", session.LoginExchange.ToEvidence("Login."));

        var guardedExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, context.BaseAddress, "harness/guarded-probe", cancellationToken: cancellationToken);
        var evidence = guardedExchange.ToEvidence("POST harness/guarded-probe with the session cookie attached and no antiforgery header.");

        if (guardedExchange.StatusCode != System.Net.HttpStatusCode.Forbidden)
            return ProbeResult.Failed($"Expected 403, got {(int)guardedExchange.StatusCode} {guardedExchange.StatusCode}.", evidence);

        return ProbeResult.Passed("A state-changing request with no antiforgery header was rejected with 403.", evidence);
    }
}
