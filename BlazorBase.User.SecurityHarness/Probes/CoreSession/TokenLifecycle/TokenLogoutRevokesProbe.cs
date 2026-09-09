using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.TokenLifecycle;

/// <summary>Refreshes with a token after that session has been logged out.</summary>
internal sealed class TokenLogoutRevokesProbe : IAuthProbe
{
    public string Id => "TOKEN-LOGOUT-REVOKES";
    public string Category => "Token lifecycle";
    public string Attack => "Logs out, then attempts to refresh with the refresh token that logout just revoked.";
    public string Expectation => "401.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("logout-revokes");
        await CoreSessionSupport.SeedUserAsync(host.Context, email);

        using var client = host.Context.HttpClientFactory();
        var (tokens, loginExchange) = await CoreSessionSupport.LoginAsync(client, host.Context.BaseAddress, email, CoreSessionSupport.SeedPassword, cancellationToken);

        if (tokens is null)
            return ProbeResult.Inconclusive("Login did not return a token pair.", loginExchange.ToEvidence("Login."));

        var logoutExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/logout",
            new RefreshTokenRequest { RefreshToken = tokens.RefreshToken }, cancellationToken: cancellationToken);

        var refreshAfterLogoutExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/refresh",
            new RefreshTokenRequest { RefreshToken = tokens.RefreshToken }, cancellationToken: cancellationToken);

        var evidence = new[]
        {
            logoutExchange.ToEvidence("Logout with the live refresh token."),
            refreshAfterLogoutExchange.ToEvidence("Refresh with that same, now-logged-out refresh token."),
        };

        if (refreshAfterLogoutExchange.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Refresh after logout answered {(int)refreshAfterLogoutExchange.StatusCode} {refreshAfterLogoutExchange.StatusCode}, not 401.", evidence);

        return ProbeResult.Passed("Refresh with a logged-out token was rejected with 401.", evidence);
    }
}
