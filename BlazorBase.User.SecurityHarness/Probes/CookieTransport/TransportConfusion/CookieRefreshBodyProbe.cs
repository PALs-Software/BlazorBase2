using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;
using BlazorBase.User.Models;

namespace BlazorBase.User.SecurityHarness.Probes.CookieTransport.TransportConfusion;

/// <summary>
/// Sends a refresh token in the request body to the cookie profile's <c>api/auth/refresh</c>.
/// <see cref="IdentityCookieAuthTransport.ReadRefreshTokenAsync"/> always answers
/// <see langword="null"/> - the cookie is the session, there is no separate refresh token to
/// present - so a body value should have no effect at all, not even to reject it as invalid; the
/// endpoint should behave exactly as if the body were empty.
/// </summary>
internal sealed class CookieRefreshBodyProbe : IAuthProbe
{
    public string Id => "COOKIE-REFRESH-BODY";
    public string Category => "Cookie transport";
    public string Attack => "Send a refresh token in the request body to the cookie profile's api/auth/refresh, with a valid cookie and antiforgery token attached.";
    public string Expectation => "Ignored; the cookie is the session.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Cookie;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();
        var session = await CookieTransportSupport.EstablishSessionAsync(context, client, "cookie-refresh-body", cancellationToken);

        if (session.Tokens is null)
            return ProbeResult.Inconclusive("Login over the cookie transport did not answer 200.", session.LoginExchange.ToEvidence("Login."));

        var (freshToken, freshTokenExchange) = await CookieTransportSupport.FetchCsrfTokenAsync(client, context.BaseAddress, cancellationToken);
        var headerName = CookieTransportSupport.GetCsrfHeaderName(context);
        var headers = new[] { new KeyValuePair<string, string>(headerName, freshToken) };

        var refreshExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, context.BaseAddress, "api/auth/refresh",
            new RefreshTokenRequest { RefreshToken = "some-other-sessions-refresh-token" }, headers: headers, cancellationToken: cancellationToken);

        var evidence = new[]
        {
            freshTokenExchange.ToEvidence("Refreshed the antiforgery token as the authenticated caller."),
            refreshExchange.ToEvidence("POST api/auth/refresh with an arbitrary refresh token in the body, over the cookie transport."),
        };

        if (refreshExchange.StatusCode == System.Net.HttpStatusCode.OK)
            return ProbeResult.Failed("The refresh endpoint answered 200 - the body's refresh token was not ignored.", evidence);

        return ProbeResult.Passed($"The body's refresh token had no effect ({(int)refreshExchange.StatusCode} {refreshExchange.StatusCode}) - the cookie transport never reads it.", evidence);
    }
}
