using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.Login;

/// <summary>Calls the setup endpoint (the only endpoint that creates a password) with an 11-character password.</summary>
internal sealed class LoginPasswordPolicyProbe : IAuthProbe
{
    public string Id => "LOGIN-PASSWORD-POLICY";
    public string Category => "Login";
    public string Attack => "Setup with an 11-character password, one below the framework's minimum length of 12.";
    public string Expectation => "Rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();

        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/setup",
            new SetupRequest { Email = CoreSessionSupport.UniqueEmail("policy"), Password = "Sh0rtPassw1", DisplayName = "Policy Probe" }, cancellationToken: cancellationToken);

        var evidence = exchange.ToEvidence("Setup with an 11-character password against this host's first-user setup.");

        if (exchange.StatusCode == System.Net.HttpStatusCode.OK)
            return ProbeResult.Failed("Setup succeeded with an 11-character password - the length-12 policy was not enforced.", evidence);

        return ProbeResult.Passed($"Setup rejected ({(int)exchange.StatusCode} {exchange.StatusCode}) - the length-12 policy held.", evidence);
    }
}
