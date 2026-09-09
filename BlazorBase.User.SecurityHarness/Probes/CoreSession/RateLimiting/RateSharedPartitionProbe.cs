using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.RateLimiting;

/// <summary>
/// Sends requests for two different accounts, all from this process's one loopback address, and
/// checks whether they draw from the same rate-limit budget.
/// </summary>
internal sealed class RateSharedPartitionProbe : IAuthProbe
{
    private const int PermitLimit = 4;

    public string Id => "RATE-SHARED-PARTITION";
    public string Category => "Rate limiting";
    public string Attack => "Two different accounts, both attempting login from one address, together exceeding the permit limit.";
    public string Expectation => "Documents that one attacker can exhaust the rate-limit budget for every account behind the same address - the limiter partitions on address, not account.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Info;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var overrides = new Dictionary<string, string?>
        {
            ["AuthTokens:RateLimit:PermitLimit"] = PermitLimit.ToString(),
            ["AuthTokens:RateLimit:WindowSeconds"] = "60",
        };
        await using var host = await context.StartIsolatedHostAsync(overrides, cancellationToken);
        var accountA = CoreSessionSupport.UniqueEmail("shared-a");
        var accountB = CoreSessionSupport.UniqueEmail("shared-b");
        await CoreSessionSupport.SeedUserAsync(host.Context, accountA);
        await CoreSessionSupport.SeedUserAsync(host.Context, accountB);

        using var client = host.Context.HttpClientFactory();
        var evidence = new List<ProbeEvidence>();

        for (var request = 1; request <= PermitLimit; request++)
        {
            var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/login",
                new LoginRequest { Email = accountA, Password = CoreSessionSupport.SeedPassword }, cancellationToken: cancellationToken);
            evidence.Add(exchange.ToEvidence($"Account A, request {request} of {PermitLimit} (budget-exhausting)."));
        }

        var accountBAttempt = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/login",
            new LoginRequest { Email = accountB, Password = CoreSessionSupport.SeedPassword }, cancellationToken: cancellationToken);
        evidence.Add(accountBAttempt.ToEvidence("Account B's own, otherwise-legitimate login attempt, after account A alone exhausted the shared budget."));
        var accountBBlocked = accountBAttempt.StatusCode == System.Net.HttpStatusCode.TooManyRequests;

        if (!accountBBlocked)
            return ProbeResult.Failed($"Account B's login still succeeded ({(int)accountBAttempt.StatusCode}) after account A alone exhausted the {PermitLimit}-request budget - the two accounts did not share a partition, contrary to the address-only partition key the framework documents.", evidence);

        return ProbeResult.Passed($"Confirmed: account A alone exhausting the {PermitLimit}-request budget also blocked account B's legitimate login from the same address - the documented address-only partitioning trade-off.", evidence);
    }
}
