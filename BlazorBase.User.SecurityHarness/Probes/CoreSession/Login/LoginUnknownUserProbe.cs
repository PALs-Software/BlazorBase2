using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.Login;

/// <summary>Logs in with an email that has never been registered.</summary>
internal sealed class LoginUnknownUserProbe : IAuthProbe
{
    public string Id => "LOGIN-UNKNOWN-USER";
    public string Category => "Login";
    public string Attack => "Login with an email that does not exist.";
    public string Expectation => "Same status and body shape as LOGIN-WRONG-PASSWORD - an attacker cannot tell an account exists from the response.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        var knownEmail = CoreSessionSupport.UniqueEmail("known");
        await CoreSessionSupport.SeedUserAsync(host.Context, knownEmail);
        var unknownEmail = CoreSessionSupport.UniqueEmail("unknown");

        using var client = host.Context.HttpClientFactory();

        var wrongPassword = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/login",
            new LoginRequest { Email = knownEmail, Password = "definitely-wrong-1A" }, cancellationToken: cancellationToken);
        var unknownUser = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/login",
            new LoginRequest { Email = unknownEmail, Password = "definitely-wrong-1A" }, cancellationToken: cancellationToken);

        var evidence = new[]
        {
            wrongPassword.ToEvidence("Known email, wrong password."),
            unknownUser.ToEvidence("Unknown email."),
        };

        if (wrongPassword.StatusCode != unknownUser.StatusCode)
            return ProbeResult.Failed($"Status codes differ: known-user-wrong-password={((int)wrongPassword.StatusCode)}, unknown-user={((int)unknownUser.StatusCode)}.", evidence);

        if (wrongPassword.ResponseBody != unknownUser.ResponseBody)
            return ProbeResult.Failed("Response bodies differ between an existing account with a wrong password and a non-existent account - an enumeration oracle.", evidence);

        return ProbeResult.Passed($"Both cases answered {(int)wrongPassword.StatusCode} with an identical body.", evidence);
    }
}
