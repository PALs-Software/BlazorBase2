using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.Login;

/// <summary>Logs in with correct credentials for an account whose <c>IsActive</c> flag is false.</summary>
internal sealed class LoginInactiveUserProbe : IAuthProbe
{
    public string Id => "LOGIN-INACTIVE-USER";
    public string Category => "Login";
    public string Attack => "Correct credentials, but the account has IsActive = false.";
    public string Expectation => "Rejected, despite the password being correct.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("inactive");
        await CoreSessionSupport.SeedUserAsync(host.Context, email, isActive: false);

        using var client = host.Context.HttpClientFactory();
        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/login",
            new LoginRequest { Email = email, Password = CoreSessionSupport.SeedPassword }, cancellationToken: cancellationToken);

        var evidence = exchange.ToEvidence("Correct password against an IsActive = false account.");

        if (exchange.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Expected 401, got {(int)exchange.StatusCode} {exchange.StatusCode} - an inactive account signed in.", evidence);

        return ProbeResult.Passed("Inactive account rejected despite the correct password.", evidence);
    }
}
