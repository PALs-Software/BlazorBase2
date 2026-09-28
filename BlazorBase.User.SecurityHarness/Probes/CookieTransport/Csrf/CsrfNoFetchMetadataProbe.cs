using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.CookieTransport.Csrf;

/// <summary>
/// A state-changing request with a valid cookie and a valid antiforgery token, but with the
/// <c>Sec-Fetch-Site</c> header omitted entirely, as a non-browser client (or a browser old enough
/// to predate fetch metadata) would send it. Records the outcome precisely rather than rounding it
/// to "safe": if an absent header is treated the same as <c>same-origin</c>, then whatever stops a
/// forged request here is the antiforgery token alone, and a client that can suppress fetch metadata
/// bypasses half the defence.
/// </summary>
internal sealed class CsrfNoFetchMetadataProbe : IAuthProbe
{
    public string Id => "CSRF-NO-FETCH-METADATA";
    public string Category => "CSRF";
    public string Attack => "A state-changing request with a valid cookie and a valid antiforgery token, and no Sec-Fetch-Site header at all.";
    public string Expectation => "Record the outcome precisely - an absent header treated as same-origin means the token check alone stands guard.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.Cookie;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();
        var session = await CookieTransportSupport.EstablishSessionAsync(context, client, "csrf-no-fetch-metadata", cancellationToken);

        if (session.Tokens is null)
            return ProbeResult.Inconclusive("Login over the cookie transport did not answer 200.", session.LoginExchange.ToEvidence("Login."));

        var (freshToken, freshTokenExchange) = await CookieTransportSupport.FetchCsrfTokenAsync(client, context.BaseAddress, cancellationToken);
        var headerName = CookieTransportSupport.GetCsrfHeaderName(context);
        var headers = new[] { new KeyValuePair<string, string>(headerName, freshToken) };
        var guardedExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, context.BaseAddress, "harness/guarded-probe", headers: headers, cancellationToken: cancellationToken);

        var evidence = new[]
        {
            freshTokenExchange.ToEvidence("Refreshed the antiforgery token as the authenticated caller."),
            guardedExchange.ToEvidence("POST harness/guarded-probe with a valid cookie and a valid token, Sec-Fetch-Site header entirely absent."),
        };

        if (guardedExchange.StatusCode == System.Net.HttpStatusCode.OK)
            return ProbeResult.Failed(
                "An absent Sec-Fetch-Site header is treated as same-origin: the request was accepted on the antiforgery token alone, with no fetch-metadata signal at all - a client that can suppress the header (any non-browser HTTP client) bypasses that half of the defence entirely.",
                evidence);

        return ProbeResult.Passed($"An absent Sec-Fetch-Site header did not let the request through ({(int)guardedExchange.StatusCode} {guardedExchange.StatusCode}).", evidence);
    }
}
