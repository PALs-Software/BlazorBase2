using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.CookieTransport.Csrf;

/// <summary>A syntactically valid antiforgery token, but one bound to a different session's antiforgery cookie.</summary>
internal sealed class CsrfWrongTokenProbe : IAuthProbe
{
    public string Id => "CSRF-WRONG-TOKEN";
    public string Category => "CSRF";
    public string Attack => "POST a state-changing request with one session's cookie but a valid antiforgery token minted for a different session.";
    public string Expectation => "Rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Cookie;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var clientA = context.HttpClientFactory();
        using var clientB = context.HttpClientFactory();

        var sessionA = await CookieTransportSupport.EstablishSessionAsync(context, clientA, "csrf-wrong-token-a", cancellationToken);
        var (foreignToken, foreignSessionExchange) = await CookieTransportSupport.FetchCsrfTokenAsync(clientB, context.BaseAddress, cancellationToken);

        if (sessionA.Tokens is null)
            return ProbeResult.Inconclusive("Login for session A did not answer 200.", sessionA.LoginExchange.ToEvidence("Login as session A."));

        var headerName = CookieTransportSupport.GetCsrfHeaderName(context);
        var headers = new[] { new KeyValuePair<string, string>(headerName, foreignToken) };
        var guardedExchange = await CoreSessionSupport.SendAsync(clientA, HttpMethod.Post, context.BaseAddress, "harness/guarded-probe", headers: headers, cancellationToken: cancellationToken);

        var evidence = new[]
        {
            foreignSessionExchange.ToEvidence("Session B's anonymous GET api/auth/session, source of the foreign token."),
            guardedExchange.ToEvidence($"POST harness/guarded-probe with session A's cookie but session B's antiforgery token ({headerName}: {foreignToken})."),
        };

        if (guardedExchange.StatusCode != System.Net.HttpStatusCode.Forbidden)
            return ProbeResult.Failed($"Expected 403, got {(int)guardedExchange.StatusCode} {guardedExchange.StatusCode}.", evidence);

        return ProbeResult.Passed("A token minted for a different session was rejected with 403.", evidence);
    }
}
