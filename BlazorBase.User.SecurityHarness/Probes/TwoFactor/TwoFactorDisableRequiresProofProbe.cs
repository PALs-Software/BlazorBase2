using System.Net;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.TwoFactor;

/// <summary>Disabling two-factor with no code, and with a wrong code, must both be rejected - regression cover for an earlier fix.</summary>
internal sealed class TwoFactorDisableRequiresProofProbe : IAuthProbe
{
    public string Id => "2FA-DISABLE-REQUIRES-PROOF";
    public string Category => "Two-factor";
    public string Attack => "Calls 2fa/disable with an empty code, and again with a wrong code.";
    public string Expectation => "Both rejected; two-factor stays enabled.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();

        var account = await TwoFactorSupport.EnableTwoFactorAsync(context, client, "2fa-disable-proof", cancellationToken);
        var (pendingToken, loginExchange) = await TwoFactorSupport.LoginExpectingPendingAsync(client, context.BaseAddress, account.Email, cancellationToken);

        if (loginExchange.StatusCode != HttpStatusCode.Accepted)
            return ProbeResult.Inconclusive("The account did not answer 202 for the pending-login precondition.", loginExchange.ToEvidence("Login."));

        var validCode = Totp.ComputeCode(account.AuthenticatorSecret, DateTimeOffset.UtcNow);
        var completion = await TwoFactorSupport.CompleteTwoFactorAsync(client, context.BaseAddress, pendingToken, validCode, isRecoveryCode: false, cancellationToken);
        CoreSessionSupport.TryDeserialize<LoginResponse>(completion.ResponseBody, out var session);

        if (completion.StatusCode != HttpStatusCode.OK || session is null)
            return ProbeResult.Inconclusive("Could not complete the second factor to obtain a bearer token for the disable attempt.", completion.ToEvidence("Completion."));

        var emptyCodeAttempt = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, context.BaseAddress, "api/auth/manage/2fa/disable",
            new TwoFactorVerificationRequest { Code = "" }, bearerToken: session.AccessToken, cancellationToken: cancellationToken);

        var wrongCodeAttempt = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, context.BaseAddress, "api/auth/manage/2fa/disable",
            new TwoFactorVerificationRequest { Code = "000000" }, bearerToken: session.AccessToken, cancellationToken: cancellationToken);

        var statusExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Get, context.BaseAddress, "api/auth/manage/2fa", bearerToken: session.AccessToken, cancellationToken: cancellationToken);
        CoreSessionSupport.TryDeserialize<TwoFactorStatusResponse>(statusExchange.ResponseBody, out var status);

        var evidence = new List<ProbeEvidence>
        {
            emptyCodeAttempt.ToEvidence("2fa/disable with an empty code."),
            wrongCodeAttempt.ToEvidence("2fa/disable with a wrong code."),
            statusExchange.ToEvidence("Two-factor status after both attempts."),
        };

        var failures = new List<string>();

        if (emptyCodeAttempt.StatusCode == HttpStatusCode.OK)
            failures.Add("an empty code disabled two-factor");

        if (wrongCodeAttempt.StatusCode == HttpStatusCode.OK)
            failures.Add("a wrong code disabled two-factor");

        if (status?.Enabled != true)
            failures.Add("two-factor is no longer enabled after both attempts");

        if (failures.Count > 0)
            return ProbeResult.Failed(string.Join("; ", failures) + ".", evidence);

        return ProbeResult.Passed("Both an empty and a wrong code were rejected by 2fa/disable, and two-factor remains enabled.", evidence);
    }
}
