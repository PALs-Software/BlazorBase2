using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.RateLimiting;

/// <summary>
/// Exhausts the permit budget, then checks whether the fixed-window rate limiter honours the
/// DI-registered <see cref="TimeProvider"/> before deciding how to observe recovery - advancing the
/// harness clock first, and only falling back to a real (short, configured) wait if that has no
/// effect.
/// </summary>
internal sealed class RateWindowRecoveryProbe : IAuthProbe
{
    private const int PermitLimit = 2;
    private const int ShortWindowSeconds = 2;

    public string Id => "RATE-WINDOW-RECOVERY";
    public string Category => "Rate limiting";
    public string Attack => "Exhausts the permit budget, then waits out the window.";
    public string Expectation => "Requests are accepted again once the window has passed.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var overrides = new Dictionary<string, string?>
        {
            ["AuthTokens:RateLimit:PermitLimit"] = PermitLimit.ToString(),
            ["AuthTokens:RateLimit:WindowSeconds"] = ShortWindowSeconds.ToString(),
        };
        await using var host = await context.StartIsolatedHostAsync(overrides, cancellationToken);
        using var client = host.Context.HttpClientFactory();
        var evidence = new List<ProbeEvidence>();

        for (var request = 1; request <= PermitLimit + 1; request++)
        {
            var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/login",
                new LoginRequest { Email = CoreSessionSupport.UniqueEmail("rate-window"), Password = "definitely-wrong-1A" }, cancellationToken: cancellationToken);
            evidence.Add(exchange.ToEvidence($"Exhausting request {request} of {PermitLimit + 1}."));

            if (request == PermitLimit + 1 && exchange.StatusCode != System.Net.HttpStatusCode.TooManyRequests)
                return ProbeResult.Inconclusive($"The budget-exhausting request never triggered 429 (got {(int)exchange.StatusCode}); recovery cannot be measured against a limit that never engaged.", evidence);
        }

        var harnessTimeProvider = (HarnessTimeProvider)host.Context.TimeProvider;
        harnessTimeProvider.Advance(TimeSpan.FromSeconds(ShortWindowSeconds * 10));

        var afterClockAdvance = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/login",
            new LoginRequest { Email = CoreSessionSupport.UniqueEmail("rate-window"), Password = "definitely-wrong-1A" }, cancellationToken: cancellationToken);
        evidence.Add(afterClockAdvance.ToEvidence($"Request after advancing the harness TimeProvider by {ShortWindowSeconds * 10}s (far past the {ShortWindowSeconds}-second configured window) without any real wait."));

        var timeProviderHonoured = afterClockAdvance.StatusCode != System.Net.HttpStatusCode.TooManyRequests;

        if (timeProviderHonoured)
            return ProbeResult.Passed("The fixed-window rate limiter honours the DI-registered TimeProvider: advancing it past the window let a request through with no real wait.", evidence);

        await Task.Delay(TimeSpan.FromSeconds(ShortWindowSeconds + 1), cancellationToken);

        var afterRealWait = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/login",
            new LoginRequest { Email = CoreSessionSupport.UniqueEmail("rate-window"), Password = "definitely-wrong-1A" }, cancellationToken: cancellationToken);
        evidence.Add(afterRealWait.ToEvidence($"Request after a real {ShortWindowSeconds + 1}-second wait (the configured window is {ShortWindowSeconds}s)."));

        if (afterRealWait.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            return ProbeResult.Failed($"Still 429 after a real wait past the configured {ShortWindowSeconds}-second window - the window never recovered.", evidence);

        return ProbeResult.Passed($"The fixed-window rate limiter does NOT honour the DI-registered TimeProvider (advancing it {ShortWindowSeconds * 10}s changed nothing); it uses the real wall clock, confirmed by a real {ShortWindowSeconds + 1}-second wait letting a request through.", evidence);
    }
}
