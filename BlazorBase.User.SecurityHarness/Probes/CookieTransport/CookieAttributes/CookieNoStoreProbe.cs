using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.CookieTransport.CookieAttributes;

/// <summary>Every token-bearing <c>api/auth/*</c> response must refuse caching.</summary>
internal sealed class CookieNoStoreProbe : IAuthProbe
{
    public string Id => "COOKIE-NO-STORE";
    public string Category => "Cookie transport";
    public string Attack => "Read response headers on the anonymous session call and the login call over the cookie transport.";
    public string Expectation => "Cache-Control: no-store (or equivalent) on every token-bearing response.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.Cookie;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();
        var session = await CookieTransportSupport.EstablishSessionAsync(host.Context, client, "cookie-no-store", cancellationToken);

        var evidence = new[]
        {
            session.SessionExchange.ToEvidence("Anonymous GET api/auth/session, which hands out the antiforgery token."),
            session.LoginExchange.ToEvidence("POST api/auth/login, which sets the session cookie."),
        };

        var missingNoStore = new List<string>();

        if (!CarriesNoStore(session.SessionExchange))
            missingNoStore.Add("api/auth/session");

        if (!CarriesNoStore(session.LoginExchange))
            missingNoStore.Add("api/auth/login");

        if (missingNoStore.Count > 0)
            return ProbeResult.Failed($"No Cache-Control: no-store on: {string.Join(", ", missingNoStore)}.", evidence);

        return ProbeResult.Passed("Both the session and login responses carry Cache-Control: no-store.", evidence);
    }

    private static bool CarriesNoStore(ProbeExchange exchange) =>
        exchange.ResponseHeaders.TryGetValues("Cache-Control", out var values)
            && values.Any(value => value.Contains("no-store", StringComparison.OrdinalIgnoreCase));
}
