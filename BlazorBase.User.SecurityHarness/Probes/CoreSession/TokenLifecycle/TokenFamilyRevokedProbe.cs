using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.TokenLifecycle;

/// <summary>
/// After a replay is detected on a consumed refresh token, presents the newest (still valid, never
/// replayed) refresh token from the same family.
/// </summary>
internal sealed class TokenFamilyRevokedProbe : IAuthProbe
{
    public string Id => "TOKEN-FAMILY-REVOKED";
    public string Category => "Token lifecycle";
    public string Attack => "Replays a consumed refresh token (triggering family revocation), then uses the newest, otherwise-still-valid refresh token from the same family.";
    public string Expectation => "Also 401 - reuse detection must revoke the whole family, not just the replayed token.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("family-revoked");
        await CoreSessionSupport.SeedUserAsync(host.Context, email);

        using var client = host.Context.HttpClientFactory();
        var (originalTokens, loginExchange) = await CoreSessionSupport.LoginAsync(client, host.Context.BaseAddress, email, CoreSessionSupport.SeedPassword, cancellationToken);

        if (originalTokens is null)
            return ProbeResult.Inconclusive("Login did not return a token pair.", loginExchange.ToEvidence("Login."));

        var firstRefreshExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/refresh",
            new RefreshTokenRequest { RefreshToken = originalTokens.RefreshToken }, cancellationToken: cancellationToken);

        if (!CoreSessionSupport.TryDeserialize<LoginResponse>(firstRefreshExchange.ResponseBody, out var newestTokens) || newestTokens is null)
            return ProbeResult.Inconclusive("The legitimate rotation did not return a token pair; family revocation cannot be measured.", firstRefreshExchange.ToEvidence("Legitimate rotation."));

        var replayExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/refresh",
            new RefreshTokenRequest { RefreshToken = originalTokens.RefreshToken }, cancellationToken: cancellationToken);

        var newestAfterReplayExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/refresh",
            new RefreshTokenRequest { RefreshToken = newestTokens.RefreshToken }, cancellationToken: cancellationToken);

        var evidence = new[]
        {
            firstRefreshExchange.ToEvidence("Legitimate rotation, producing the family's newest refresh token."),
            replayExchange.ToEvidence("Replaying the original (now-consumed) refresh token - detected reuse."),
            newestAfterReplayExchange.ToEvidence("Using the family's newest, never-replayed refresh token immediately after the reuse was detected."),
        };

        if (replayExchange.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return ProbeResult.Inconclusive($"The replay itself did not answer 401 (got {(int)replayExchange.StatusCode}); family revocation cannot be measured without it.", evidence);

        if (newestAfterReplayExchange.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"The family's newest refresh token still worked ({(int)newestAfterReplayExchange.StatusCode}) after a replay was detected on an older token from the same family.", evidence);

        return ProbeResult.Passed("Once a replay was detected, the entire refresh-token family was revoked - the newest, never-replayed token was rejected too.", evidence);
    }
}
