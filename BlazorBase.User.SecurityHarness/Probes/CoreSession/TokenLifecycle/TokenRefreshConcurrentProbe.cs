using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.TokenLifecycle;

/// <summary>Fires several refreshes with the same, still-valid refresh token at once.</summary>
internal sealed class TokenRefreshConcurrentProbe : IAuthProbe
{
    private const int ConcurrentAttempts = 5;

    public string Id => "TOKEN-REFRESH-CONCURRENT";
    public string Category => "Token lifecycle";
    public string Attack => $"{ConcurrentAttempts} concurrent refresh calls with the same refresh token.";
    public string Expectation => "Exactly one succeeds - the revoke-then-replace step must be atomic.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("refresh-concurrent");
        await CoreSessionSupport.SeedUserAsync(host.Context, email);

        using var client = host.Context.HttpClientFactory();
        var (tokens, loginExchange) = await CoreSessionSupport.LoginAsync(client, host.Context.BaseAddress, email, CoreSessionSupport.SeedPassword, cancellationToken);

        if (tokens is null)
            return ProbeResult.Inconclusive("Login did not return a token pair.", loginExchange.ToEvidence("Login."));

        var refreshTasks = Enumerable.Range(1, ConcurrentAttempts)
            .Select(_ => CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/refresh",
                new RefreshTokenRequest { RefreshToken = tokens.RefreshToken }, cancellationToken: cancellationToken))
            .ToArray();

        var results = await Task.WhenAll(refreshTasks);

        var evidence = results
            .Select((exchange, index) => exchange.ToEvidence($"Concurrent refresh attempt {index + 1} of {ConcurrentAttempts}."))
            .ToArray();

        var successCount = results.Count(exchange => exchange.StatusCode == System.Net.HttpStatusCode.OK);

        if (successCount != 1)
            return ProbeResult.Failed($"{successCount} of {ConcurrentAttempts} concurrent refreshes succeeded; expected exactly 1.", evidence);

        return ProbeResult.Passed($"Exactly 1 of {ConcurrentAttempts} concurrent refreshes with the same token succeeded; the rest were rejected.", evidence);
    }
}
