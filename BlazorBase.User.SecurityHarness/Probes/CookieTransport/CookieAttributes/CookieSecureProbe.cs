using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CookieTransport.CookieAttributes;

/// <summary>Logs in over the cookie transport and inspects the raw <c>Set-Cookie</c> header text for <c>Secure</c>.</summary>
internal sealed class CookieSecureProbe : IAuthProbe
{
    public string Id => "COOKIE-SECURE";
    public string Category => "Cookie transport";
    public string Attack => "Read the Set-Cookie header text after logging in over the cookie transport.";
    public string Expectation => "The session cookie's attributes carry Secure.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Cookie;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();
        var session = await CookieTransportSupport.EstablishSessionAsync(host.Context, client, "cookie-secure", cancellationToken);

        if (session.Tokens is null)
            return ProbeResult.Inconclusive("Login over the cookie transport did not answer 200.", session.LoginExchange.ToEvidence("Login."));

        var cookieName = CookieTransportSupport.GetSessionCookieName(host.Context);
        var setCookieHeaders = CookieTransportSupport.GetSetCookieHeaders(session.LoginExchange);
        var sessionCookieHeader = CookieTransportSupport.FindCookieHeader(setCookieHeaders, cookieName);
        var evidence = session.LoginExchange.ToEvidence($"Login response, raw Set-Cookie headers: {string.Join(" | ", setCookieHeaders)}");

        if (sessionCookieHeader is null)
            return ProbeResult.Inconclusive($"No Set-Cookie header named '{cookieName}' was present on the login response.", evidence);

        if (!sessionCookieHeader.Contains("secure", StringComparison.OrdinalIgnoreCase))
            return ProbeResult.Failed($"The session cookie's Set-Cookie header carries no Secure attribute: {sessionCookieHeader}", evidence);

        return ProbeResult.Passed($"The session cookie carries Secure: {sessionCookieHeader}", evidence);
    }
}
