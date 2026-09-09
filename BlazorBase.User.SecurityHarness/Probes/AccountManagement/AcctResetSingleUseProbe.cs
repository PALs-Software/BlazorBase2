using System.Net;
using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.AccountManagement;

/// <summary>
/// Uses the same password-reset token twice. <c>ResetPasswordAsync</c> keeps no explicit "used" flag
/// of its own - single use, if it holds, is a side effect of <see cref="Microsoft.AspNetCore.Identity.UserManager{TUser}.ResetPasswordAsync"/>
/// rotating the security stamp the token's own <c>DataProtectorTokenProvider</c> validation is bound to.
/// </summary>
internal sealed class AcctResetSingleUseProbe : IAuthProbe
{
    public string Id => "ACCT-RESET-SINGLE-USE";
    public string Category => "Account management";
    public string Attack => "Uses the same password-reset token twice, with a different new password each time.";
    public string Expectation => "The second use is rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("reset-single-use");
        var user = await CoreSessionSupport.SeedUserAsync(host.Context, email);
        var token = await AccountManagementSupport.GeneratePasswordResetTokenAsync(host.Context, user);

        using var client = host.Context.HttpClientFactory();
        var firstAttempt = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/manage/reset-password",
            new ResetPasswordRequest { Email = email, Token = token, NewPassword = "Correct-Horse-2" }, cancellationToken: cancellationToken);
        var secondAttempt = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/manage/reset-password",
            new ResetPasswordRequest { Email = email, Token = token, NewPassword = "Correct-Horse-3" }, cancellationToken: cancellationToken);

        var evidence = new[]
        {
            firstAttempt.ToEvidence("First use of the reset token."),
            secondAttempt.ToEvidence("The same reset token, used again with a different new password."),
        };

        if (firstAttempt.StatusCode != HttpStatusCode.NoContent)
            return ProbeResult.Inconclusive($"The first use did not succeed (got {(int)firstAttempt.StatusCode}); single-use cannot be measured.", evidence);

        if (secondAttempt.StatusCode == HttpStatusCode.NoContent)
            return ProbeResult.Failed("The same reset token succeeded a second time.", evidence);

        return ProbeResult.Passed("The reset token was rejected on its second use - the reset it performed the first time rotated the security stamp the token's own validation is bound to.", evidence);
    }
}
