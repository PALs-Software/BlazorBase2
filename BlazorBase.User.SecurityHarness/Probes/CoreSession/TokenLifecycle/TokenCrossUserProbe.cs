using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.TokenLifecycle;

/// <summary>
/// Refreshes with user A's own refresh token while the request's <c>Authorization</c> header
/// carries user B's access token - the one artefact of "another user's session" the bearer
/// transport can even receive, since <c>/api/auth/refresh</c> never looks at the caller's identity,
/// only at the refresh token in the body.
/// </summary>
internal sealed class TokenCrossUserProbe : IAuthProbe
{
    public string Id => "TOKEN-CROSS-USER";
    public string Category => "Token lifecycle";
    public string Attack => "User A's refresh token in the body, with an Authorization: Bearer header carrying user B's access token.";
    public string Expectation => "The rotation belongs to user A - the Authorization header must never override or merge with the refresh token actually presented.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        var emailA = CoreSessionSupport.UniqueEmail("cross-user-a");
        var emailB = CoreSessionSupport.UniqueEmail("cross-user-b");
        var userA = await CoreSessionSupport.SeedUserAsync(host.Context, emailA);
        await CoreSessionSupport.SeedUserAsync(host.Context, emailB);

        using var client = host.Context.HttpClientFactory();
        var (tokensA, loginExchangeA) = await CoreSessionSupport.LoginAsync(client, host.Context.BaseAddress, emailA, CoreSessionSupport.SeedPassword, cancellationToken);
        var (tokensB, loginExchangeB) = await CoreSessionSupport.LoginAsync(client, host.Context.BaseAddress, emailB, CoreSessionSupport.SeedPassword, cancellationToken);

        if (tokensA is null || tokensB is null)
            return ProbeResult.Inconclusive("Login for user A or user B did not return a token pair.", loginExchangeA.ToEvidence("Login as user A."), loginExchangeB.ToEvidence("Login as user B."));

        var refreshExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/refresh",
            new RefreshTokenRequest { RefreshToken = tokensA.RefreshToken }, bearerToken: tokensB.AccessToken, cancellationToken: cancellationToken);

        if (!CoreSessionSupport.TryDeserialize<LoginResponse>(refreshExchange.ResponseBody, out var rotated) || rotated is null)
            return ProbeResult.Inconclusive($"Refresh did not return a parseable token pair (status {(int)refreshExchange.StatusCode}); cross-user binding cannot be checked.", refreshExchange.ToEvidence("Refresh with user A's refresh token and user B's Authorization header."));

        var sessionExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Get, host.Context.BaseAddress, "api/auth/session",
            bearerToken: rotated.AccessToken, cancellationToken: cancellationToken);

        var evidence = new[]
        {
            refreshExchange.ToEvidence("Refresh with user A's refresh token in the body and user B's access token as the Authorization header."),
            sessionExchange.ToEvidence("Session check with the resulting access token, to see which account it actually authenticates as."),
        };

        if (!CoreSessionSupport.TryDeserialize<AuthSessionResponse>(sessionExchange.ResponseBody, out var session) || session is null)
            return ProbeResult.Inconclusive("Session endpoint did not return a parseable response.", evidence);

        var authenticatedUserId = session.Claims.FirstOrDefault(claim => claim.Type.EndsWith("nameidentifier", StringComparison.OrdinalIgnoreCase))?.Value;

        if (authenticatedUserId != userA.Id)
            return ProbeResult.Failed($"The rotated token authenticated as user id '{authenticatedUserId}', not user A's own id '{userA.Id}' - the Authorization header influenced whose session was rotated.", evidence);

        return ProbeResult.Passed("The rotation always belonged to user A, the refresh token's actual owner - the Authorization header carrying user B's token had no effect.", evidence);
    }
}

