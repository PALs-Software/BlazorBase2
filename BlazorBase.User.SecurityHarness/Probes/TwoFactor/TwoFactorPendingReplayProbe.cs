using System.Net;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.TwoFactor;

/// <summary>Reusing the pending token after it already completed a successful second factor must be rejected.</summary>
internal sealed class TwoFactorPendingReplayProbe : IAuthProbe
{
    public string Id => "2FA-PENDING-REPLAY";
    public string Category => "Two-factor";
    public string Attack => "Completes login/2fa successfully, then presents the same pending token with a fresh valid code a second time.";
    public string Expectation => "Rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();

        var account = await TwoFactorSupport.EnableTwoFactorAsync(context, client, "2fa-pending-replay", cancellationToken);
        var (pendingToken, loginExchange) = await TwoFactorSupport.LoginExpectingPendingAsync(client, context.BaseAddress, account.Email, cancellationToken);

        if (loginExchange.StatusCode != HttpStatusCode.Accepted || string.IsNullOrEmpty(pendingToken))
            return ProbeResult.Inconclusive("The initial login did not answer 202 with a pending token; replay could not be tested.", loginExchange.ToEvidence("Initial login."));

        var firstCode = Totp.ComputeCode(account.AuthenticatorSecret, DateTimeOffset.UtcNow);
        var firstCompletion = await TwoFactorSupport.CompleteTwoFactorAsync(client, context.BaseAddress, pendingToken, firstCode, isRecoveryCode: false, cancellationToken);

        var evidence = new List<ProbeEvidence> { firstCompletion.ToEvidence("First, successful completion.") };

        if (firstCompletion.StatusCode != HttpStatusCode.OK)
            return ProbeResult.Inconclusive($"The first completion did not succeed ({(int)firstCompletion.StatusCode} {firstCompletion.StatusCode}); replay could not be tested.", evidence);

        var secondCode = Totp.ComputeCode(account.AuthenticatorSecret, DateTimeOffset.UtcNow);
        var replay = await TwoFactorSupport.CompleteTwoFactorAsync(client, context.BaseAddress, pendingToken, secondCode, isRecoveryCode: false, cancellationToken);
        evidence.Add(replay.ToEvidence("Replaying the same pending token with a fresh valid code."));

        if (replay.StatusCode == HttpStatusCode.OK)
            return ProbeResult.Failed("The pending token was accepted a second time after already completing a successful second factor.", evidence);

        return ProbeResult.Passed("Reusing the pending token after a successful completion was rejected.", evidence);
    }
}
