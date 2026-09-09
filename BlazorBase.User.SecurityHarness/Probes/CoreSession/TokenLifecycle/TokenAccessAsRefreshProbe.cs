using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.TokenLifecycle;

/// <summary>Sends the access token where a refresh token is expected, and the refresh token where an access token is expected.</summary>
internal sealed class TokenAccessAsRefreshProbe : IAuthProbe
{
    public string Id => "TOKEN-ACCESS-AS-REFRESH";
    public string Category => "Token lifecycle";
    public string Attack => "Presents the access token as a refresh token to api/auth/refresh, and the refresh token as a bearer access token to a protected endpoint.";
    public string Expectation => "Rejected in both directions.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("access-as-refresh");
        await CoreSessionSupport.SeedUserAsync(host.Context, email);

        using var client = host.Context.HttpClientFactory();
        var (tokens, loginExchange) = await CoreSessionSupport.LoginAsync(client, host.Context.BaseAddress, email, CoreSessionSupport.SeedPassword, cancellationToken);

        if (tokens is null)
            return ProbeResult.Inconclusive("Login did not return a token pair.", loginExchange.ToEvidence("Login."));

        var accessAsRefresh = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/refresh",
            new RefreshTokenRequest { RefreshToken = tokens.AccessToken }, cancellationToken: cancellationToken);

        var refreshAsAccess = await CoreSessionSupport.CallProtectedEndpointAsync(client, host.Context.BaseAddress, tokens.RefreshToken, cancellationToken);

        var evidence = new[]
        {
            accessAsRefresh.ToEvidence("The access token, presented as a refresh token to api/auth/refresh."),
            refreshAsAccess.ToEvidence("The refresh token, presented as a bearer access token to a protected endpoint."),
        };

        var failures = new List<string>();

        if (accessAsRefresh.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            failures.Add($"the access token was accepted as a refresh token ({(int)accessAsRefresh.StatusCode})");

        if (refreshAsAccess.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            failures.Add($"the refresh token was accepted as a bearer access token ({(int)refreshAsAccess.StatusCode})");

        if (failures.Count > 0)
            return ProbeResult.Failed($"Cross-transport acceptance: {string.Join("; ", failures)}.", evidence);

        return ProbeResult.Passed("Both directions were rejected - an access token is not accepted as a refresh token, and a refresh token is not accepted as a bearer access token.", evidence);
    }
}
