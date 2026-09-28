using System.Net;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.TwoFactor;

/// <summary>User A's pending token combined with user B's valid code must be rejected - the pending token resolves a single fixed user, so B's code is simply wrong for it.</summary>
internal sealed class TwoFactorPendingCrossUserProbe : IAuthProbe
{
    public string Id => "2FA-PENDING-CROSS-USER";
    public string Category => "Two-factor";
    public string Attack => "Presents user A's pending token together with user B's own currently valid TOTP code.";
    public string Expectation => "Rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();

        var userA = await TwoFactorSupport.EnableTwoFactorAsync(context, client, "2fa-cross-user-a", cancellationToken);
        var userB = await TwoFactorSupport.EnableTwoFactorAsync(context, client, "2fa-cross-user-b", cancellationToken);

        var (pendingTokenA, loginExchange) = await TwoFactorSupport.LoginExpectingPendingAsync(client, context.BaseAddress, userA.Email, cancellationToken);

        if (loginExchange.StatusCode != HttpStatusCode.Accepted || string.IsNullOrEmpty(pendingTokenA))
            return ProbeResult.Inconclusive("User A's login did not answer 202 with a pending token; the cross-user attempt could not be tested.", loginExchange.ToEvidence("User A login."));

        var codeB = Totp.ComputeCode(userB.AuthenticatorSecret, DateTimeOffset.UtcNow);
        var crossUserAttempt = await TwoFactorSupport.CompleteTwoFactorAsync(client, context.BaseAddress, pendingTokenA, codeB, isRecoveryCode: false, cancellationToken);
        var evidence = crossUserAttempt.ToEvidence("User A's pending token with user B's own currently valid code.");

        if (crossUserAttempt.StatusCode == HttpStatusCode.OK)
            return ProbeResult.Failed("User B's code completed user A's pending sign-in.", evidence);

        return ProbeResult.Passed("User A's pending token was rejected when paired with user B's code.", evidence);
    }
}
