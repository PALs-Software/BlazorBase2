using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.CookieTransport.Csrf;

/// <summary>Presents the exact same valid antiforgery token twice, to see whether it is single-use.</summary>
internal sealed class CsrfReplayProbe : IAuthProbe
{
    public string Id => "CSRF-REPLAY";
    public string Category => "CSRF";
    public string Attack => "Two state-changing requests, back to back, presenting the exact same valid antiforgery token both times.";
    public string Expectation => "Record whether tokens are single-use.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Info;
    public HarnessProfile Profile => HarnessProfile.Cookie;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();
        var session = await CookieTransportSupport.EstablishSessionAsync(context, client, "csrf-replay", cancellationToken);

        if (session.Tokens is null)
            return ProbeResult.Inconclusive("Login over the cookie transport did not answer 200.", session.LoginExchange.ToEvidence("Login."));

        var (freshToken, freshTokenExchange) = await CookieTransportSupport.FetchCsrfTokenAsync(client, context.BaseAddress, cancellationToken);
        var headerName = CookieTransportSupport.GetCsrfHeaderName(context);
        var headers = new[] { new KeyValuePair<string, string>(headerName, freshToken) };

        var firstExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, context.BaseAddress, "harness/guarded-probe", headers: headers, cancellationToken: cancellationToken);
        var secondExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, context.BaseAddress, "harness/guarded-probe", headers: headers, cancellationToken: cancellationToken);

        var evidence = new[]
        {
            freshTokenExchange.ToEvidence("Refreshed the antiforgery token as the authenticated caller."),
            firstExchange.ToEvidence($"First use of the token ({headerName}: {freshToken})."),
            secondExchange.ToEvidence($"Second, identical use of the same token."),
        };

        var summary = $"First use: {(int)firstExchange.StatusCode} {firstExchange.StatusCode}. Second, identical use: {(int)secondExchange.StatusCode} {secondExchange.StatusCode}.";

        if (firstExchange.StatusCode == System.Net.HttpStatusCode.OK && secondExchange.StatusCode == System.Net.HttpStatusCode.OK)
            return ProbeResult.Passed($"{summary} Tokens are not single-use - the same token validates repeatedly, as ASP.NET Core's antiforgery tokens are designed to.", evidence);

        if (firstExchange.StatusCode == System.Net.HttpStatusCode.OK && secondExchange.StatusCode != System.Net.HttpStatusCode.OK)
            return ProbeResult.Passed($"{summary} Tokens are single-use - the second, identical use was rejected.", evidence);

        return ProbeResult.Inconclusive($"{summary} The first use did not even succeed, so single-use cannot be assessed.", evidence);
    }
}
