using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.CookieTransport.Csrf;

/// <summary>
/// A valid cookie and a valid antiforgery token, with a browser-reported same-site (different
/// subdomain of the same registrable domain) fetch, against <see cref="AuthCookieOptions"/>'s
/// default posture of not allowing it.
/// </summary>
internal sealed class CsrfSameSiteFetchProbe : IAuthProbe
{
    public string Id => "CSRF-SAME-SITE-FETCH";
    public string Category => "CSRF";
    public string Attack => "Sec-Fetch-Site: same-site, with AllowSameSiteRequests at its default (false), a valid cookie and a valid antiforgery token.";
    public string Expectation => "Rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Cookie;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();
        var session = await CookieTransportSupport.EstablishSessionAsync(context, client, "csrf-same-site", cancellationToken);

        if (session.Tokens is null)
            return ProbeResult.Inconclusive("Login over the cookie transport did not answer 200.", session.LoginExchange.ToEvidence("Login."));

        var (freshToken, freshTokenExchange) = await CookieTransportSupport.FetchCsrfTokenAsync(client, context.BaseAddress, cancellationToken);
        var headerName = CookieTransportSupport.GetCsrfHeaderName(context);
        var headers = new[]
        {
            new KeyValuePair<string, string>(headerName, freshToken),
            new KeyValuePair<string, string>("Sec-Fetch-Site", "same-site"),
        };
        var guardedExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, context.BaseAddress, "harness/guarded-probe", headers: headers, cancellationToken: cancellationToken);

        var evidence = new[]
        {
            freshTokenExchange.ToEvidence("Refreshed the antiforgery token as the authenticated caller."),
            guardedExchange.ToEvidence("POST harness/guarded-probe with a valid cookie, a valid token, and Sec-Fetch-Site: same-site."),
        };

        if (guardedExchange.StatusCode != System.Net.HttpStatusCode.Forbidden)
            return ProbeResult.Failed($"Expected 403, got {(int)guardedExchange.StatusCode} {guardedExchange.StatusCode}.", evidence);

        return ProbeResult.Passed("A same-site fetch was rejected with 403 by default, even with a valid token.", evidence);
    }
}
