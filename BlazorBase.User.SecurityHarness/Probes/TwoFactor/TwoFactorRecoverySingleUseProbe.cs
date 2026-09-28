using System.Net;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.TwoFactor;

/// <summary>A recovery code must not work a second time.</summary>
internal sealed class TwoFactorRecoverySingleUseProbe : IAuthProbe
{
    public string Id => "2FA-RECOVERY-SINGLE-USE";
    public string Category => "Two-factor";
    public string Attack => "Uses one recovery code to complete login/2fa, then presents the same recovery code again on a fresh pending login.";
    public string Expectation => "Rejected the second time.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();

        var account = await TwoFactorSupport.EnableTwoFactorAsync(context, client, "2fa-recovery-single-use", cancellationToken);

        if (account.RecoveryCodes.Count == 0)
            return ProbeResult.Inconclusive("No recovery codes were returned by enrolment; single-use could not be tested.");

        var recoveryCode = account.RecoveryCodes[0];

        var (pendingToken1, login1) = await TwoFactorSupport.LoginExpectingPendingAsync(client, context.BaseAddress, account.Email, cancellationToken);

        if (login1.StatusCode != HttpStatusCode.Accepted || string.IsNullOrEmpty(pendingToken1))
            return ProbeResult.Inconclusive("The first login did not answer 202 with a pending token; single-use could not be tested.", login1.ToEvidence("First login."));

        var firstUse = await TwoFactorSupport.CompleteTwoFactorAsync(client, context.BaseAddress, pendingToken1, recoveryCode, isRecoveryCode: true, cancellationToken);
        var evidence = new List<ProbeEvidence> { firstUse.ToEvidence("Using the recovery code the first time.") };

        if (firstUse.StatusCode != HttpStatusCode.OK)
            return ProbeResult.Inconclusive($"The first use of the recovery code did not succeed ({(int)firstUse.StatusCode} {firstUse.StatusCode}); single-use could not be tested.", evidence);

        var (pendingToken2, login2) = await TwoFactorSupport.LoginExpectingPendingAsync(client, context.BaseAddress, account.Email, cancellationToken);
        evidence.Add(login2.ToEvidence("Second login, to obtain a fresh pending token."));

        if (login2.StatusCode != HttpStatusCode.Accepted || string.IsNullOrEmpty(pendingToken2))
            return ProbeResult.Inconclusive("The second login did not answer 202 with a pending token; single-use could not be tested.", evidence);

        var secondUse = await TwoFactorSupport.CompleteTwoFactorAsync(client, context.BaseAddress, pendingToken2, recoveryCode, isRecoveryCode: true, cancellationToken);
        evidence.Add(secondUse.ToEvidence("Presenting the same recovery code a second time."));

        if (secondUse.StatusCode == HttpStatusCode.OK)
            return ProbeResult.Failed("The same recovery code completed login a second time.", evidence);

        return ProbeResult.Passed("The recovery code was rejected on its second use.", evidence);
    }
}
