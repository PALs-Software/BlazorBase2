using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.AccountManagement;

/// <summary>
/// Calls <c>development-login</c> on an isolated instance of the <c>DevelopmentLogin</c> profile
/// (Development environment) with <c>DevelopmentAuthentication:Enabled</c> overridden back to
/// <see langword="false"/> - the feature registered, but switched off.
/// </summary>
internal sealed class DevLoginAbsentWhenDisabledProbe : IAuthProbe
{
    public string Id => "DEV-LOGIN-ABSENT-WHEN-DISABLED";
    public string Category => "Development login";
    public string Attack => "Calls development-login in the Development environment with the feature explicitly switched off.";
    public string Expectation => "404.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.DevelopmentLogin;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var overrides = new Dictionary<string, string?> { ["DevelopmentAuthentication:Enabled"] = "false" };
        await using var host = await context.StartIsolatedHostAsync(overrides, cancellationToken);
        using var client = host.Context.HttpClientFactory();

        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/development-login", cancellationToken: cancellationToken);
        var evidence = exchange.ToEvidence("Development login, Development environment, feature explicitly disabled.");

        if (exchange.StatusCode != System.Net.HttpStatusCode.NotFound)
            return ProbeResult.Failed($"Expected 404, got {(int)exchange.StatusCode}.", evidence);

        return ProbeResult.Passed("The endpoint answered 404 with the feature explicitly disabled.", evidence);
    }
}
