using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.CookieTransport.Csrf;

/// <summary>
/// Fetches an antiforgery token while anonymous, uses it to log in - the only way login can succeed
/// at all, since <c>api/auth/login</c> carries no CSRF exemption - and then presents that exact same
/// token again for a second state-changing request, now that the caller is authenticated. Records
/// whether the framework's own documented claim on <c>AuthSessionResponse.CsrfToken</c> - that a
/// token is "bound to this caller's identity" - actually holds across the anonymous-to-authenticated
/// transition, rather than only across two anonymous callers.
/// </summary>
internal sealed class CsrfAnonymousTokenReuseProbe : IAuthProbe
{
    public string Id => "CSRF-ANONYMOUS-TOKEN-REUSE";
    public string Category => "CSRF";
    public string Attack => "Fetch a CSRF token while anonymous, log in with it, then reuse that same token for a state-changing request after login.";
    public string Expectation => "Record the outcome; a token that survives the privilege change is a finding.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Cookie;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();
        var session = await CookieTransportSupport.EstablishSessionAsync(context, client, "csrf-anon-reuse", cancellationToken);

        if (session.Tokens is null)
            return ProbeResult.Inconclusive("Login with the anonymously-fetched token did not answer 200 - the token could not even be used to log in.", session.LoginExchange.ToEvidence("Login."));

        var headerName = CookieTransportSupport.GetCsrfHeaderName(context);
        var headers = new[] { new KeyValuePair<string, string>(headerName, session.CsrfToken) };
        var postLoginExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, context.BaseAddress, "harness/guarded-probe", headers: headers, cancellationToken: cancellationToken);

        var evidence = new[]
        {
            session.SessionExchange.ToEvidence("Anonymous GET api/auth/session, minting the token."),
            session.LoginExchange.ToEvidence($"POST api/auth/login using that token ({headerName}: {session.CsrfToken})."),
            postLoginExchange.ToEvidence($"POST harness/guarded-probe, now authenticated, reusing the exact same token ({headerName}: {session.CsrfToken})."),
        };

        if (postLoginExchange.StatusCode == System.Net.HttpStatusCode.OK)
            return ProbeResult.Failed("The token minted while anonymous still validates after the caller authenticated - it did not become bound to the new identity.", evidence);

        return ProbeResult.Passed($"The token minted while anonymous no longer validates after authentication ({(int)postLoginExchange.StatusCode} {postLoginExchange.StatusCode}).", evidence);
    }
}
