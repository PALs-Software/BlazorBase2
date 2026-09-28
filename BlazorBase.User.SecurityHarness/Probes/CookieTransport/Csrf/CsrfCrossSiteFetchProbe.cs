using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.CookieTransport.Csrf;

/// <summary>A valid cookie and a valid antiforgery token, but a browser-reported cross-site fetch.</summary>
internal sealed class CsrfCrossSiteFetchProbe : IAuthProbe
{
    public string Id => "CSRF-CROSS-SITE-FETCH";
    public string Category => "CSRF";
    public string Attack => "Sec-Fetch-Site: cross-site, with a valid cookie and a valid antiforgery token.";
    public string Expectation => "Rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Cookie;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();
        var session = await CookieTransportSupport.EstablishSessionAsync(context, client, "csrf-cross-site", cancellationToken);

        if (session.Tokens is null)
            return ProbeResult.Inconclusive("Login over the cookie transport did not answer 200.", session.LoginExchange.ToEvidence("Login."));

        var (freshToken, freshTokenExchange) = await CookieTransportSupport.FetchCsrfTokenAsync(client, context.BaseAddress, cancellationToken);
        var headerName = CookieTransportSupport.GetCsrfHeaderName(context);
        var headers = new[]
        {
            new KeyValuePair<string, string>(headerName, freshToken),
            new KeyValuePair<string, string>("Sec-Fetch-Site", "cross-site"),
        };
        var guardedExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, context.BaseAddress, "harness/guarded-probe", headers: headers, cancellationToken: cancellationToken);

        var evidence = new[]
        {
            freshTokenExchange.ToEvidence("Refreshed the antiforgery token as the authenticated caller."),
            guardedExchange.ToEvidence($"POST harness/guarded-probe with a valid cookie, a valid token, and Sec-Fetch-Site: cross-site."),
        };

        if (guardedExchange.StatusCode != System.Net.HttpStatusCode.Forbidden)
            return ProbeResult.Failed($"Expected 403, got {(int)guardedExchange.StatusCode} {guardedExchange.StatusCode}.", evidence);

        return ProbeResult.Passed("A cross-site fetch was rejected with 403 even with a valid token.", evidence);
    }
}
