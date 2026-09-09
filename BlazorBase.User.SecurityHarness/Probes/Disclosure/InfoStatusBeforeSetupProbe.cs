using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.Disclosure;

/// <summary>
/// Calls <c>api/auth/status</c> anonymously on a freshly started, never-seeded host and records
/// exactly what it reveals before anyone has an account - <see cref="AuthStatusResponse.HasUsers"/>
/// is a deliberate first-run signal the client needs to decide whether to show a setup form, not an
/// accidental leak.
/// </summary>
internal sealed class InfoStatusBeforeSetupProbe : IAuthProbe
{
    public string Id => "INFO-STATUS-BEFORE-SETUP";
    public string Category => "Disclosure";
    public string Attack => "GET api/auth/status on a freshly started host, before any account exists.";
    public string Expectation => "Records what it reveals before setup - HasUsers=false, DevelopmentAuthenticationEnabled=false on a Bearer host.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Info;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();

        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Get, host.Context.BaseAddress, "api/auth/status", cancellationToken: cancellationToken);
        var evidence = exchange.ToEvidence("Anonymous api/auth/status call on a freshly started, never-seeded host.");

        if (!CoreSessionSupport.TryDeserialize<AuthStatusResponse>(exchange.ResponseBody, out var status) || status is null)
            return ProbeResult.Inconclusive("The status response could not be parsed.", evidence);

        return ProbeResult.Passed(
            $"Before any account exists, an anonymous caller learns HasUsers={status.HasUsers} and DevelopmentAuthenticationEnabled={status.DevelopmentAuthenticationEnabled} - a deliberate first-run signal, not an accidental leak.",
            evidence);
    }
}
