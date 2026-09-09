using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CookieTransport.CookieAttributes;

/// <summary>The whole point of the cookie transport: the login body carries no usable token at all.</summary>
internal sealed class CookieNoTokenInBodyProbe : IAuthProbe
{
    public string Id => "COOKIE-NO-TOKEN-IN-BODY";
    public string Category => "Cookie transport";
    public string Attack => "Read the login response body over the cookie transport.";
    public string Expectation => "The body carries no access or refresh token.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Cookie;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();
        var session = await CookieTransportSupport.EstablishSessionAsync(host.Context, client, "cookie-no-token", cancellationToken);
        var evidence = session.LoginExchange.ToEvidence("Login response body over the cookie transport.");

        if (session.Tokens is null)
            return ProbeResult.Inconclusive("Login over the cookie transport did not answer 200.", evidence);

        if (!string.IsNullOrEmpty(session.Tokens.AccessToken))
            return ProbeResult.Failed($"The login body carries a non-empty access token: '{session.Tokens.AccessToken}'.", evidence);

        if (!string.IsNullOrEmpty(session.Tokens.RefreshToken))
            return ProbeResult.Failed($"The login body carries a non-empty refresh token: '{session.Tokens.RefreshToken}'.", evidence);

        return ProbeResult.Passed("The login body's access and refresh token fields are both empty.", evidence);
    }
}
