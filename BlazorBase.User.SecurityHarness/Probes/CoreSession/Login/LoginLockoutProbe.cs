using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.Login;

/// <summary>
/// Repeats a wrong password until ASP.NET Core Identity's default lockout threshold
/// (<c>MaxFailedAccessAttempts = 5</c>, unmodified by this framework) trips, then tries the correct
/// password once more.
/// </summary>
internal sealed class LoginLockoutProbe : IAuthProbe
{
    private const int FailedAttemptsToTriggerLockout = 5;

    public string Id => "LOGIN-LOCKOUT";
    public string Category => "Login";
    public string Attack => "Repeated wrong passwords against one account.";
    public string Expectation => "The account locks, and the correct password then also fails.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("lockout");
        await CoreSessionSupport.SeedUserAsync(host.Context, email);

        using var client = host.Context.HttpClientFactory();
        var evidence = new List<ProbeEvidence>();

        for (var attempt = 1; attempt <= FailedAttemptsToTriggerLockout; attempt++)
        {
            var wrongAttempt = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/login",
                new LoginRequest { Email = email, Password = "definitely-wrong-1A" }, cancellationToken: cancellationToken);
            evidence.Add(wrongAttempt.ToEvidence($"Wrong-password attempt {attempt} of {FailedAttemptsToTriggerLockout}."));
        }

        var correctAfterLockout = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/login",
            new LoginRequest { Email = email, Password = CoreSessionSupport.SeedPassword }, cancellationToken: cancellationToken);
        evidence.Add(correctAfterLockout.ToEvidence("Correct password immediately after the lockout threshold was reached."));

        if (correctAfterLockout.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Correct password succeeded ({(int)correctAfterLockout.StatusCode}) despite {FailedAttemptsToTriggerLockout} prior failures - the account never locked.", evidence);

        return ProbeResult.Passed($"After {FailedAttemptsToTriggerLockout} wrong attempts, the correct password was also rejected (401) - the account is locked.", evidence);
    }
}
