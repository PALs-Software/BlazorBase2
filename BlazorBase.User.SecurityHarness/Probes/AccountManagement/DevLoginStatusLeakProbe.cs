using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.AccountManagement;

/// <summary>
/// Calls <c>api/auth/status</c> anonymously against the <c>DevelopmentLogin</c> profile and records
/// that <see cref="AuthStatusResponse.DevelopmentAuthenticationEnabled"/> tells any caller, before
/// they authenticate, whether development login is available - a deliberate disclosure the client
/// uses to sign itself in without a login form, not an accidental one.
/// </summary>
internal sealed class DevLoginStatusLeakProbe : IAuthProbe
{
    public string Id => "DEV-LOGIN-STATUS-LEAK";
    public string Category => "Development login";
    public string Attack => "Calls api/auth/status anonymously against a host with development login enabled.";
    public string Expectation => "Records that the response advertises whether development login exists, by design.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Info;
    public HarnessProfile Profile => HarnessProfile.DevelopmentLogin;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        using var client = context.HttpClientFactory();
        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Get, context.BaseAddress, "api/auth/status", cancellationToken: cancellationToken);
        var evidence = exchange.ToEvidence("Anonymous api/auth/status call against a host with development login enabled.");

        if (!CoreSessionSupport.TryDeserialize<AuthStatusResponse>(exchange.ResponseBody, out var status) || status is null)
            return ProbeResult.Inconclusive("The status response could not be parsed.", evidence);

        return ProbeResult.Passed(
            $"api/auth/status reported DevelopmentAuthenticationEnabled={status.DevelopmentAuthenticationEnabled} to an anonymous caller - a deliberate disclosure that lets a client sign itself in without a login form, always false outside Development.",
            evidence);
    }
}
