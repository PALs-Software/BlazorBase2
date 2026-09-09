using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CookieTransport.CookieAttributes;

/// <summary>
/// Logs in over the cookie transport (HTTPS) and checks that the session cookie's name and
/// attributes together satisfy the <c>__Host-</c> prefix's promise: <c>Secure</c>, <c>Path=/</c>, and
/// no <c>Domain</c> - a browser enforces that promise itself and refuses to store a cookie whose name
/// carries the prefix but whose attributes do not satisfy it, so this is a check the server-side code
/// alone can silently violate without ever seeing the browser reject it.
/// </summary>
internal sealed class CookieHostPrefixProbe : IAuthProbe
{
    public string Id => "COOKIE-HOST-PREFIX";
    public string Category => "Cookie transport";
    public string Attack => "Read the Set-Cookie header text after logging in over the cookie transport, over HTTPS.";
    public string Expectation => "The cookie name carries the __Host- prefix, and the attributes satisfy it: Secure, Path=/, no Domain.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Cookie;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();
        var session = await CookieTransportSupport.EstablishSessionAsync(host.Context, client, "cookie-host-prefix", cancellationToken);

        if (session.Tokens is null)
            return ProbeResult.Inconclusive("Login over the cookie transport did not answer 200.", session.LoginExchange.ToEvidence("Login."));

        var cookieName = CookieTransportSupport.GetSessionCookieName(host.Context);
        var setCookieHeaders = CookieTransportSupport.GetSetCookieHeaders(session.LoginExchange);
        var sessionCookieHeader = CookieTransportSupport.FindCookieHeader(setCookieHeaders, cookieName);
        var evidence = session.LoginExchange.ToEvidence($"Login response, raw Set-Cookie headers: {string.Join(" | ", setCookieHeaders)}");

        if (sessionCookieHeader is null)
            return ProbeResult.Inconclusive($"No Set-Cookie header named '{cookieName}' was present on the login response.", evidence);

        if (!cookieName.StartsWith("__Host-", StringComparison.Ordinal))
            return ProbeResult.Failed($"The session cookie's name '{cookieName}' does not carry the __Host- prefix.", evidence);

        var missingAttributes = new List<string>();

        if (!sessionCookieHeader.Contains("secure", StringComparison.OrdinalIgnoreCase))
            missingAttributes.Add("Secure");

        if (!sessionCookieHeader.Contains("path=/", StringComparison.OrdinalIgnoreCase))
            missingAttributes.Add("Path=/");

        if (sessionCookieHeader.Contains("domain=", StringComparison.OrdinalIgnoreCase))
            missingAttributes.Add("no Domain (one is present)");

        if (missingAttributes.Count > 0)
            return ProbeResult.Failed($"Cookie name carries __Host- but the attributes do not satisfy it - missing/violating: {string.Join(", ", missingAttributes)}. {sessionCookieHeader}", evidence);

        return ProbeResult.Passed($"__Host- prefix and its required attributes are both present: {sessionCookieHeader}", evidence);
    }
}
