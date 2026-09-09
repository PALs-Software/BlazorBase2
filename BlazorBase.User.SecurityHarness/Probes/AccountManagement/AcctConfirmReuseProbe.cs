using System.Net;
using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.AccountManagement;

/// <summary>
/// Uses the same email-confirmation token twice. Unlike a password reset, confirming an email never
/// rotates the security stamp the token's validation is bound to - so, unlike
/// <see cref="AcctResetSingleUseProbe"/>, there is no a-priori reason to expect the second use to be
/// rejected. Records what actually happens rather than assuming either direction.
/// </summary>
internal sealed class AcctConfirmReuseProbe : IAuthProbe
{
    public string Id => "ACCT-CONFIRM-REUSE";
    public string Category => "Account management";
    public string Attack => "Uses the same email-confirmation token twice.";
    public string Expectation => "Recorded either way - confirming an email does not rotate the security stamp the token is bound to, so reuse is not a-priori expected to fail.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Low;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("confirm-reuse");
        var user = await CoreSessionSupport.SeedUserAsync(host.Context, email);
        var token = await AccountManagementSupport.GenerateEmailConfirmationTokenAsync(host.Context, user);

        using var client = host.Context.HttpClientFactory();
        var firstAttempt = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/manage/confirm-email",
            new ConfirmEmailRequest { UserId = user.Id, Token = token }, cancellationToken: cancellationToken);
        var secondAttempt = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/manage/confirm-email",
            new ConfirmEmailRequest { UserId = user.Id, Token = token }, cancellationToken: cancellationToken);

        var evidence = new[]
        {
            firstAttempt.ToEvidence("First use of the confirmation token."),
            secondAttempt.ToEvidence("The same confirmation token, used again."),
        };

        if (firstAttempt.StatusCode != HttpStatusCode.OK)
            return ProbeResult.Inconclusive($"The first use did not succeed (got {(int)firstAttempt.StatusCode}); reuse cannot be measured.", evidence);

        var reused = secondAttempt.StatusCode == HttpStatusCode.OK;

        return ProbeResult.Inconclusive(
            reused
                ? "The confirmation token was accepted a second time - it carries no single-use tracking of its own and email confirmation does not rotate the security stamp its validation is bound to, unlike a password reset."
                : $"The confirmation token was rejected on its second use (got {(int)secondAttempt.StatusCode}).",
            evidence);
    }
}
