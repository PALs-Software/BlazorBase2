using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.CookieTransport.TransportConfusion;

/// <summary>
/// Logs out and then presents the pre-logout cookie value again, both to a protected route and to
/// <c>api/auth/session</c>, and separately checks that the logout response's own <c>Set-Cookie</c>
/// header actually clears the cookie client-side. These are two independent questions: whether the
/// server tells the browser to forget the cookie, and whether the server itself still honours that
/// exact cookie value if a copy of it - captured before logout - is presented again.
/// </summary>
internal sealed class CookieLogoutClearsProbe : IAuthProbe
{
    public string Id => "COOKIE-LOGOUT-CLEARS";
    public string Category => "Cookie transport";
    public string Attack => "Log out, then present the pre-logout cookie value again.";
    public string Expectation => "Rejected, and the cookie is actually cleared.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Cookie;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();
        var session = await CookieTransportSupport.EstablishSessionAsync(context, client, "cookie-logout-clears", cancellationToken);

        if (session.Tokens is null)
            return ProbeResult.Inconclusive("Login over the cookie transport did not answer 200.", session.LoginExchange.ToEvidence("Login."));

        var cookieName = CookieTransportSupport.GetSessionCookieName(context);
        var preLogoutSessionCookie = CookieTransportSupport.FindCookieHeader(CookieTransportSupport.GetSetCookieHeaders(session.LoginExchange), cookieName);

        if (preLogoutSessionCookie is null)
            return ProbeResult.Inconclusive($"No Set-Cookie header named '{cookieName}' was present on the login response.", session.LoginExchange.ToEvidence("Login."));

        var (logoutCsrfToken, logoutCsrfExchange) = await CookieTransportSupport.FetchCsrfTokenAsync(client, context.BaseAddress, cancellationToken);
        var headerName = CookieTransportSupport.GetCsrfHeaderName(context);
        var logoutHeaders = new[] { new KeyValuePair<string, string>(headerName, logoutCsrfToken) };
        var logoutExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, context.BaseAddress, "api/auth/logout",
            headers: logoutHeaders, cancellationToken: cancellationToken);

        var logoutSetCookieHeaders = CookieTransportSupport.GetSetCookieHeaders(logoutExchange);
        var clearingCookieHeader = CookieTransportSupport.FindCookieHeader(logoutSetCookieHeaders, cookieName);
        var cookieWasCleared = clearingCookieHeader is not null && clearingCookieHeader.Contains("expires=", StringComparison.OrdinalIgnoreCase);

        var replayHeaders = new[] { new KeyValuePair<string, string>("Cookie", preLogoutSessionCookie.Split(';')[0]) };
        var replayExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Get, context.BaseAddress, "api/auth/session", headers: replayHeaders, cancellationToken: cancellationToken);
        CoreSessionSupport.TryDeserialize<AuthSessionResponse>(replayExchange.ResponseBody, out var replaySession);
        var replayStillAuthenticates = replaySession?.IsAuthenticated == true;

        var evidence = new[]
        {
            logoutCsrfExchange.ToEvidence("Refreshed the antiforgery token to present with logout."),
            logoutExchange.ToEvidence($"POST api/auth/logout. Set-Cookie headers on the response: {string.Join(" | ", logoutSetCookieHeaders)}"),
            replayExchange.ToEvidence($"GET api/auth/session, manually presenting the pre-logout cookie value ({preLogoutSessionCookie.Split(';')[0]}) again after logout."),
        };

        if (!cookieWasCleared)
            return ProbeResult.Failed(
                $"Logout answered {(int)logoutExchange.StatusCode} {logoutExchange.StatusCode}. Its Set-Cookie header for '{cookieName}' does not clear the cookie (no Expires in the past): " +
                $"{clearingCookieHeader ?? "(no such Set-Cookie header at all)"}. Replaying the pre-logout cookie afterward {(replayStillAuthenticates ? "still authenticates" : "no longer authenticates")}.",
                evidence);

        if (replayStillAuthenticates)
            return ProbeResult.Failed("The pre-logout cookie value still authenticates after logout, even though the response told the browser to clear it - the server itself did not revoke that cookie value.", evidence);

        return ProbeResult.Passed("Logout clears the cookie client-side, and the pre-logout cookie value no longer authenticates.", evidence);
    }
}
