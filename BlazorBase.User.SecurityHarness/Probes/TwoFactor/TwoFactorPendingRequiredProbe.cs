using System.Net;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.TwoFactor;

/// <summary>Canary for the whole category: a password login for a two-factor-enabled account must answer pending, never a session.</summary>
internal sealed class TwoFactorPendingRequiredProbe : IAuthProbe
{
    public string Id => "2FA-PENDING-REQUIRED";
    public string Category => "Two-factor";
    public string Attack => "Logs in with the correct password for an account with two-factor enabled.";
    public string Expectation => "202 with a pending token, never a session. Canary for every other two-factor probe.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();

        var account = await TwoFactorSupport.EnableTwoFactorAsync(context, client, "2fa-pending-required", cancellationToken);
        var (pendingToken, exchange) = await TwoFactorSupport.LoginExpectingPendingAsync(client, context.BaseAddress, account.Email, cancellationToken);
        var evidence = exchange.ToEvidence("POST api/auth/login for a two-factor-enabled account.");

        if (exchange.StatusCode != HttpStatusCode.Accepted)
            return ProbeResult.Failed($"Expected 202, got {(int)exchange.StatusCode} {exchange.StatusCode}. Every other two-factor probe's result is meaningless until this is fixed.", evidence);

        if (string.IsNullOrEmpty(pendingToken))
            return ProbeResult.Failed("A 202 was returned but it carried no pending token.", evidence);

        return ProbeResult.Passed("Login for a two-factor-enabled account answered 202 with a pending token, not a session.", evidence);
    }
}
