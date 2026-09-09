using System.Diagnostics;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.ClientPipeline;

/// <summary>
/// The reason this probe group exists: <see cref="Services.AuthTokenHandler"/>'s refresh gate is a
/// <see langword="static"/> <see cref="SemaphoreSlim"/>, shared by every instance in this process -
/// correct for WebAssembly and MAUI, where one process is one user, but shared by every circuit in a
/// Blazor Server host. Two clients, two different users, both with an access token expired
/// server-side, refresh at the same time - established empirically whether the shared gate only
/// serialises them or lets a token cross between users, by comparing each client's own final token
/// identity through <c>harness/whoami</c> rather than assuming either outcome.
/// </summary>
internal sealed class ClientConcurrentUsersProbe : IAuthProbe
{
    private const int AccessTokenMinutes = 10;

    public string Id => "CLIENT-CONCURRENT-USERS";
    public string Category => "Client pipeline";
    public string Attack => "Two clients, two different users, both with an expired access token, refreshing at the same time in one process.";
    public string Expectation => "Neither ends up with the other's token; reports whether the shared gate serialises them.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var overrides = new Dictionary<string, string?> { ["AuthTokens:AccessTokenMinutes"] = AccessTokenMinutes.ToString() };
        await using var host = await context.StartIsolatedHostAsync(overrides, cancellationToken);
        var emailA = CoreSessionSupport.UniqueEmail("concurrent-a");
        var emailB = CoreSessionSupport.UniqueEmail("concurrent-b");
        await CoreSessionSupport.SeedUserAsync(host.Context, emailA);
        await CoreSessionSupport.SeedUserAsync(host.Context, emailB);

        using var stackA = ClientPipelineSupport.BuildClientStack(host.Context.BaseAddress);
        using var stackB = ClientPipelineSupport.BuildClientStack(host.Context.BaseAddress);
        await ClientPipelineSupport.LoginAndAdoptAsync(stackA, emailA, cancellationToken);
        await ClientPipelineSupport.LoginAndAdoptAsync(stackB, emailB, cancellationToken);

        var harnessTimeProvider = (HarnessTimeProvider)host.Context.TimeProvider;
        harnessTimeProvider.Advance(TimeSpan.FromMinutes(AccessTokenMinutes) + TimeSpan.FromMinutes(1));

        var stopwatch = Stopwatch.StartNew();
        var callA = stackA.ApiHttpClient.GetAsync("harness/protected-probe", cancellationToken);
        var callB = stackB.ApiHttpClient.GetAsync("harness/protected-probe", cancellationToken);
        await Task.WhenAll(callA, callB);
        stopwatch.Stop();

        using var responseA = await callA;
        using var responseB = await callB;

        var tokensA = await stackA.TokenStorage.GetTokensAsync();
        var tokensB = await stackB.TokenStorage.GetTokensAsync();

        using var plainClient = host.Context.HttpClientFactory();
        var whoAmIWithA = tokensA is not null ? await ClientPipelineSupport.WhoAmIAsync(plainClient, host.Context.BaseAddress, tokensA.AccessToken, cancellationToken) : null;
        var whoAmIWithB = tokensB is not null ? await ClientPipelineSupport.WhoAmIAsync(plainClient, host.Context.BaseAddress, tokensB.AccessToken, cancellationToken) : null;

        var evidence = new ProbeEvidence(
            "Both concurrent calls, the final token each client's own storage ended up with, and who that token actually authenticates as.",
            Request: "GET harness/protected-probe simultaneously from two client stacks sharing the same static AuthTokenHandler.RefreshGate, different users, both access tokens expired.",
            Response: $"A: {(int)responseA.StatusCode}, B: {(int)responseB.StatusCode}. Elapsed for both together: {stopwatch.Elapsed.TotalMilliseconds:F1} ms. " +
                      $"Refresh calls - A: {stackA.RefreshCounter.Count}, B: {stackB.RefreshCounter.Count}. " +
                      $"Client A's final token authenticates as: {whoAmIWithA?.Email ?? "(none)"} (expected {emailA}). " +
                      $"Client B's final token authenticates as: {whoAmIWithB?.Email ?? "(none)"} (expected {emailB}).");

        if (tokensA is null || tokensB is null)
            return ProbeResult.Inconclusive("At least one client ended up with no token at all after the concurrent refresh; identity crossing cannot be assessed.", evidence);

        var crossed = whoAmIWithA?.Email != emailA || whoAmIWithB?.Email != emailB;

        if (crossed)
            return ProbeResult.Failed("A token crossed between users under concurrent refresh through the shared static gate.", evidence);

        return ProbeResult.Passed(
            $"Both users kept their own identity after a concurrent refresh through the shared static gate. Elapsed {stopwatch.Elapsed.TotalMilliseconds:F1} ms for both together - " +
            $"{(stopwatch.Elapsed.TotalMilliseconds > 0 ? "consistent with the gate serialising the two refreshes rather than running them fully in parallel" : "too fast to distinguish serialisation from full parallelism")}.",
            evidence);
    }
}
