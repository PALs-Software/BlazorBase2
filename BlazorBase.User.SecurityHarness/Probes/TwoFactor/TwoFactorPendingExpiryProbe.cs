using System.Net;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.TwoFactor;

/// <summary>
/// A pending token used after its 5-minute lifetime must be rejected.
/// <c>TwoFactorPendingTokenService</c> resolves its expiry check from the DI-registered
/// <see cref="TimeProvider"/>, so advancing the harness clock is enough - no real wait needed.
/// </summary>
internal sealed class TwoFactorPendingExpiryProbe : IAuthProbe
{
    public string Id => "2FA-PENDING-EXPIRY";
    public string Category => "Two-factor";
    public string Attack => "Advances the harness clock 10 minutes past login, then presents the pending token with a valid code.";
    public string Expectation => "Rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();

        var account = await TwoFactorSupport.EnableTwoFactorAsync(context, client, "2fa-pending-expiry", cancellationToken);
        var (pendingToken, loginExchange) = await TwoFactorSupport.LoginExpectingPendingAsync(client, context.BaseAddress, account.Email, cancellationToken);

        if (loginExchange.StatusCode != HttpStatusCode.Accepted || string.IsNullOrEmpty(pendingToken))
            return ProbeResult.Inconclusive("The initial login did not answer 202 with a pending token; expiry could not be tested.", loginExchange.ToEvidence("Initial login."));

        var harnessTimeProvider = (HarnessTimeProvider)context.TimeProvider;
        harnessTimeProvider.Advance(TimeSpan.FromMinutes(10));

        var code = Totp.ComputeCode(account.AuthenticatorSecret, DateTimeOffset.UtcNow);
        var afterExpiry = await TwoFactorSupport.CompleteTwoFactorAsync(client, context.BaseAddress, pendingToken, code, isRecoveryCode: false, cancellationToken);
        var evidence = afterExpiry.ToEvidence("Presenting the pending token and a valid code after advancing the harness TimeProvider 10 minutes (past the 5-minute pending-token lifetime), no real wait.");

        if (afterExpiry.StatusCode == HttpStatusCode.OK)
            return ProbeResult.Failed("An expired pending token was still accepted.", evidence);

        return ProbeResult.Passed("A pending token used after its lifetime was rejected.", evidence);
    }
}
