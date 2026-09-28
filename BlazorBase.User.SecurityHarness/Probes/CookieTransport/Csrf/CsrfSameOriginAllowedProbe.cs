using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.CookieTransport.Csrf;

/// <summary>The positive control: a real same-origin request with a valid token must not be blocked by accident.</summary>
internal sealed class CsrfSameOriginAllowedProbe : IAuthProbe
{
    public string Id => "CSRF-SAME-ORIGIN-ALLOWED";
    public string Category => "CSRF";
    public string Attack => "Sec-Fetch-Site: same-origin, with a valid cookie and a valid antiforgery token.";
    public string Expectation => "Accepted - the control must not be so strict that the real client breaks.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Cookie;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();
        var session = await CookieTransportSupport.EstablishSessionAsync(context, client, "csrf-same-origin", cancellationToken);

        if (session.Tokens is null)
            return ProbeResult.Inconclusive("Login over the cookie transport did not answer 200.", session.LoginExchange.ToEvidence("Login."));

        var (freshToken, freshTokenExchange) = await CookieTransportSupport.FetchCsrfTokenAsync(client, context.BaseAddress, cancellationToken);
        var headerName = CookieTransportSupport.GetCsrfHeaderName(context);
        var headers = new[]
        {
            new KeyValuePair<string, string>(headerName, freshToken),
            new KeyValuePair<string, string>("Sec-Fetch-Site", "same-origin"),
        };
        var guardedExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, context.BaseAddress, "harness/guarded-probe", headers: headers, cancellationToken: cancellationToken);

        var evidence = new[]
        {
            freshTokenExchange.ToEvidence("Refreshed the antiforgery token as the authenticated caller."),
            guardedExchange.ToEvidence("POST harness/guarded-probe with a valid cookie, a valid token, and Sec-Fetch-Site: same-origin."),
        };

        if (guardedExchange.StatusCode != System.Net.HttpStatusCode.OK)
            return ProbeResult.Failed($"Expected 200, got {(int)guardedExchange.StatusCode} {guardedExchange.StatusCode} - the real client's own requests would be blocked.", evidence);

        return ProbeResult.Passed("A genuine same-origin request with a valid token was accepted.", evidence);
    }
}
