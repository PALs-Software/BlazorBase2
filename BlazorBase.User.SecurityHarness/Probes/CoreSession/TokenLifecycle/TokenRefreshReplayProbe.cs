using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.TokenLifecycle;

/// <summary>Presents a refresh token a second time, after it has already been rotated away.</summary>
internal sealed class TokenRefreshReplayProbe : IAuthProbe
{
    public string Id => "TOKEN-REFRESH-REPLAY";
    public string Category => "Token lifecycle";
    public string Attack => "Refreshes once (consuming the token), then presents that same, now-revoked refresh token again.";
    public string Expectation => "401 on the replayed token.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("refresh-replay");
        await CoreSessionSupport.SeedUserAsync(host.Context, email);

        using var client = host.Context.HttpClientFactory();
        var (tokens, loginExchange) = await CoreSessionSupport.LoginAsync(client, host.Context.BaseAddress, email, CoreSessionSupport.SeedPassword, cancellationToken);

        if (tokens is null)
            return ProbeResult.Inconclusive("Login did not return a token pair.", loginExchange.ToEvidence("Login."));

        var firstRefresh = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/refresh",
            new RefreshTokenRequest { RefreshToken = tokens.RefreshToken }, cancellationToken: cancellationToken);

        if (firstRefresh.StatusCode != System.Net.HttpStatusCode.OK)
            return ProbeResult.Inconclusive($"The first refresh did not succeed ({(int)firstRefresh.StatusCode}); replay cannot be measured.", firstRefresh.ToEvidence("First refresh, consuming the original token."));

        var replay = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/refresh",
            new RefreshTokenRequest { RefreshToken = tokens.RefreshToken }, cancellationToken: cancellationToken);
        var evidence = new[]
        {
            firstRefresh.ToEvidence("First refresh, consuming the original token."),
            replay.ToEvidence("The same, now-revoked refresh token, presented a second time."),
        };

        if (replay.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Replaying the consumed refresh token answered {(int)replay.StatusCode} {replay.StatusCode}, not 401.", evidence);

        return ProbeResult.Passed("Replaying the already-consumed refresh token was rejected with 401.", evidence);
    }
}
