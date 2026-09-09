using System.Net;
using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.TwoFactor;

/// <summary>Regenerating recovery codes without proof of possession must be rejected.</summary>
internal sealed class TwoFactorRegenerateRequiresProofProbe : IAuthProbe
{
    public string Id => "2FA-REGENERATE-REQUIRES-PROOF";
    public string Category => "Two-factor";
    public string Attack => "Calls 2fa/recovery-codes with an empty code, and again with a wrong code.";
    public string Expectation => "Both rejected; no new recovery codes are issued.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();

        var account = await TwoFactorSupport.EnableTwoFactorAsync(context, client, "2fa-regen-proof", cancellationToken);
        var (pendingToken, loginExchange) = await TwoFactorSupport.LoginExpectingPendingAsync(client, context.BaseAddress, account.Email, cancellationToken);

        if (loginExchange.StatusCode != HttpStatusCode.Accepted)
            return ProbeResult.Inconclusive("The account did not answer 202 for the pending-login precondition.", loginExchange.ToEvidence("Login."));

        var validCode = Totp.ComputeCode(account.AuthenticatorSecret, DateTimeOffset.UtcNow);
        var completion = await TwoFactorSupport.CompleteTwoFactorAsync(client, context.BaseAddress, pendingToken, validCode, isRecoveryCode: false, cancellationToken);
        CoreSessionSupport.TryDeserialize<LoginResponse>(completion.ResponseBody, out var session);

        if (completion.StatusCode != HttpStatusCode.OK || session is null)
            return ProbeResult.Inconclusive("Could not complete the second factor to obtain a bearer token.", completion.ToEvidence("Completion."));

        var emptyCodeAttempt = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, context.BaseAddress, "api/auth/manage/2fa/recovery-codes",
            new TwoFactorVerificationRequest { Code = "" }, bearerToken: session.AccessToken, cancellationToken: cancellationToken);

        var wrongCodeAttempt = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, context.BaseAddress, "api/auth/manage/2fa/recovery-codes",
            new TwoFactorVerificationRequest { Code = "000000" }, bearerToken: session.AccessToken, cancellationToken: cancellationToken);

        var evidence = new List<ProbeEvidence>
        {
            emptyCodeAttempt.ToEvidence("2fa/recovery-codes with an empty code."),
            wrongCodeAttempt.ToEvidence("2fa/recovery-codes with a wrong code."),
        };

        var failures = new List<string>();

        if (emptyCodeAttempt.StatusCode == HttpStatusCode.OK)
            failures.Add("an empty code regenerated recovery codes");

        if (wrongCodeAttempt.StatusCode == HttpStatusCode.OK)
            failures.Add("a wrong code regenerated recovery codes");

        if (failures.Count > 0)
            return ProbeResult.Failed(string.Join("; ", failures) + ".", evidence);

        return ProbeResult.Passed("Both an empty and a wrong code were rejected by 2fa/recovery-codes.", evidence);
    }
}
