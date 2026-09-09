using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.CookieTransport.TransportConfusion;

/// <summary>
/// Presents an <c>Authorization: Bearer</c> header - which <see cref="BlazorBaseCsrfMiddleware"/>
/// treats as proof of intent and exempts from the CSRF check entirely, regardless of whether the
/// token it carries is valid for anything - against an authenticated route, while the live session
/// cookie is also attached. The policy scheme's <c>ForwardDefaultSelector</c> routes any request that
/// carries an <c>Authorization</c> header to the bearer scheme instead of the cookie scheme, so the
/// question is whether a garbage bearer value can still ride on the cookie's identity, or whether
/// forcing the bearer scheme also forces bearer-only authentication and the caller ends up with
/// neither.
/// </summary>
internal sealed class CookieBearerDowngradeProbe : IAuthProbe
{
    public string Id => "COOKIE-BEARER-DOWNGRADE";
    public string Category => "Cookie transport";
    public string Attack => "Present an Authorization: Bearer header (an arbitrary, invalid value) alongside a valid session cookie, against an authenticated route.";
    public string Expectation => "The policy scheme must not let a caller pick the weaker path around the CSRF check and still authenticate as the cookie's user.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Cookie;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();
        var session = await CookieTransportSupport.EstablishSessionAsync(context, client, "cookie-bearer-downgrade", cancellationToken);

        if (session.Tokens is null)
            return ProbeResult.Inconclusive("Login over the cookie transport did not answer 200.", session.LoginExchange.ToEvidence("Login."));

        var downgradeExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, context.BaseAddress, "harness/protected-guarded-probe",
            bearerToken: "not-a-valid-bearer-token", cancellationToken: cancellationToken);

        var evidence = downgradeExchange.ToEvidence("POST harness/protected-guarded-probe with the live session cookie attached and an arbitrary, invalid Authorization: Bearer header.");

        if (downgradeExchange.StatusCode == System.Net.HttpStatusCode.OK)
            return ProbeResult.Failed("An arbitrary Authorization header bypassed the CSRF check (as designed) and the request still authenticated as the cookie's user - the CSRF-bypass exemption can be ridden by anyone able to attach any header value at all.", evidence);

        return ProbeResult.Passed($"An arbitrary Authorization header bypassed the CSRF check but did not authenticate the request ({(int)downgradeExchange.StatusCode} {downgradeExchange.StatusCode}) - forcing the bearer scheme also forces bearer-only authentication.", evidence);
    }
}
