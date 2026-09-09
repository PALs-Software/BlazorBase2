using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.Login;

/// <summary>
/// Locks an account out, then checks whether ASP.NET Core Identity's lockout end honours the
/// DI-registered <see cref="TimeProvider"/> before deciding how to observe recovery: advancing the
/// harness clock past the lockout window first, and only falling back to a real (short, configured)
/// wait if that has no effect.
/// </summary>
internal sealed class LoginLockoutResetsProbe : IAuthProbe
{
    private const int FailedAttemptsToTriggerLockout = 5;
    private const int ShortLockoutSeconds = 2;

    public string Id => "LOGIN-LOCKOUT-RESETS";
    public string Category => "Login";
    public string Attack => "Waits out the lockout window, then presents the correct password again.";
    public string Expectation => "The correct password works again once the lockout window has passed.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var overrides = new Dictionary<string, string?> { ["AuthTokens:LockoutSeconds"] = ShortLockoutSeconds.ToString() };
        await using var host = await context.StartIsolatedHostAsync(overrides, cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("lockout-resets");
        await CoreSessionSupport.SeedUserAsync(host.Context, email);

        using var client = host.Context.HttpClientFactory();
        var evidence = new List<ProbeEvidence>();

        for (var attempt = 1; attempt <= FailedAttemptsToTriggerLockout; attempt++)
        {
            var wrongAttempt = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/login",
                new LoginRequest { Email = email, Password = "definitely-wrong-1A" }, cancellationToken: cancellationToken);
            evidence.Add(wrongAttempt.ToEvidence($"Wrong-password attempt {attempt} of {FailedAttemptsToTriggerLockout}."));
        }

        var harnessTimeProvider = (HarnessTimeProvider)host.Context.TimeProvider;
        harnessTimeProvider.Advance(TimeSpan.FromMinutes(10));

        var afterClockAdvance = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/login",
            new LoginRequest { Email = email, Password = CoreSessionSupport.SeedPassword }, cancellationToken: cancellationToken);
        evidence.Add(afterClockAdvance.ToEvidence("Correct password after advancing the harness TimeProvider by 10 minutes (far past the 2-second configured lockout) without any real wait."));

        var timeProviderHonoured = afterClockAdvance.StatusCode == System.Net.HttpStatusCode.OK;

        if (timeProviderHonoured)
            return ProbeResult.Passed("Identity's lockout honours the DI-registered TimeProvider: advancing it past the lockout window let the correct password through with no real wait.", evidence);

        await Task.Delay(TimeSpan.FromSeconds(ShortLockoutSeconds + 1), cancellationToken);

        var afterRealWait = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/login",
            new LoginRequest { Email = email, Password = CoreSessionSupport.SeedPassword }, cancellationToken: cancellationToken);
        evidence.Add(afterRealWait.ToEvidence($"Correct password after a real {ShortLockoutSeconds + 1}-second wait (the configured lockout window is {ShortLockoutSeconds}s)."));

        if (afterRealWait.StatusCode != System.Net.HttpStatusCode.OK)
            return ProbeResult.Failed($"The correct password was still rejected ({(int)afterRealWait.StatusCode}) after a real wait past the configured {ShortLockoutSeconds}-second lockout window.", evidence);

        return ProbeResult.Passed($"Identity's lockout does NOT honour the DI-registered TimeProvider (advancing it 10 minutes changed nothing); it uses the real wall clock, confirmed by a real {ShortLockoutSeconds + 1}-second wait letting the correct password through.", evidence);
    }
}
