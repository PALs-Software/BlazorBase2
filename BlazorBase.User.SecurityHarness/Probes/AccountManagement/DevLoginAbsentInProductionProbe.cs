using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.AccountManagement;

/// <summary>
/// Calls <c>development-login</c> against the <c>Bearer</c> profile - a Production-environment host
/// (see <see cref="Hosting.HarnessHostFactory"/>'s environment switch) that never registers
/// development authentication at all, the default and safe configuration for a real deployment.
/// </summary>
internal sealed class DevLoginAbsentInProductionProbe : IAuthProbe
{
    public string Id => "DEV-LOGIN-ABSENT-IN-PRODUCTION";
    public string Category => "Development login";
    public string Attack => "Calls development-login against a Production-environment host that never registered development authentication.";
    public string Expectation => "404 - the route resolves, but no IDevelopmentSessionService is registered to answer it.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        using var client = context.HttpClientFactory();
        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, context.BaseAddress, "api/auth/development-login", cancellationToken: cancellationToken);
        var evidence = exchange.ToEvidence("Development login against a Production-environment host with no development authentication registered.");

        if (exchange.StatusCode != System.Net.HttpStatusCode.NotFound)
            return ProbeResult.Failed($"Expected 404, got {(int)exchange.StatusCode}.", evidence);

        return ProbeResult.Passed("The endpoint answered 404 in Production with development authentication never registered.", evidence);
    }
}
