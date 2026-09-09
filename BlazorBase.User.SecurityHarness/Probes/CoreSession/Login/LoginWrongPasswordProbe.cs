using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.Login;

/// <summary>Logs in with the correct email and an incorrect password.</summary>
internal sealed class LoginWrongPasswordProbe : IAuthProbe
{
    public string Id => "LOGIN-WRONG-PASSWORD";
    public string Category => "Login";
    public string Attack => "Correct email, wrong password.";
    public string Expectation => "401 Unauthorized, and the body carries no token.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("wrong-password");
        await CoreSessionSupport.SeedUserAsync(host.Context, email);

        using var client = host.Context.HttpClientFactory();
        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/login",
            new LoginRequest { Email = email, Password = "definitely-wrong-1A" }, cancellationToken: cancellationToken);

        var evidence = exchange.ToEvidence("Login with the correct email and a wrong password.");

        if (exchange.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Expected 401, got {(int)exchange.StatusCode} {exchange.StatusCode}.", evidence);

        if (exchange.ResponseBody.Contains("accessToken", StringComparison.OrdinalIgnoreCase))
            return ProbeResult.Failed("401 body carries an access token.", evidence);

        return ProbeResult.Passed("Wrong password rejected with 401 and no token in the body.", evidence);
    }
}
