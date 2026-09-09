using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.AccountManagement;

/// <summary>
/// The canary for this probe group: calls <c>development-login</c> against the <c>DevelopmentLogin</c>
/// profile - Development environment, the feature switched on. If this fails, every other probe in
/// this group is meaningless.
/// </summary>
internal sealed class DevLoginWorksProbe : IAuthProbe
{
    public string Id => "DEV-LOGIN-WORKS";
    public string Category => "Development login";
    public string Attack => "Calls development-login against a Development-environment host with the feature enabled.";
    public string Expectation => "Mints a real token pair for the configured development account. Canary for this group.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.DevelopmentLogin;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        using var client = context.HttpClientFactory();
        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, context.BaseAddress, "api/auth/development-login", cancellationToken: cancellationToken);
        var evidence = exchange.ToEvidence("Development login.");

        if (exchange.StatusCode != System.Net.HttpStatusCode.OK)
            return ProbeResult.Failed($"Canary failed: expected 200, got {(int)exchange.StatusCode}. Every other probe in this group is meaningless until this is fixed.", evidence);

        if (!CoreSessionSupport.TryDeserialize<LoginResponse>(exchange.ResponseBody, out var tokens) || tokens is null || string.IsNullOrEmpty(tokens.AccessToken))
            return ProbeResult.Failed("Canary failed: the 200 response did not carry a usable token pair.", evidence);

        return ProbeResult.Passed("The development-login endpoint minted a real token pair.", evidence);
    }
}
