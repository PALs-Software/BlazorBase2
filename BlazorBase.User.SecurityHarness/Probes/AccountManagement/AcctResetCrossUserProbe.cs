using System.Net;
using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.AccountManagement;

/// <summary>
/// Presents user A's password-reset token together with user B's email address.
/// <c>ResetPasswordAsync</c> resolves the user from the request's own <c>Email</c> field before
/// verifying the token against that resolved user, so a token minted for A should never validate
/// against B.
/// </summary>
internal sealed class AcctResetCrossUserProbe : IAuthProbe
{
    public string Id => "ACCT-RESET-CROSS-USER";
    public string Category => "Account management";
    public string Attack => "Presents user A's password-reset token with user B's email address.";
    public string Expectation => "Rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        var emailA = CoreSessionSupport.UniqueEmail("reset-cross-a");
        var emailB = CoreSessionSupport.UniqueEmail("reset-cross-b");
        var userA = await CoreSessionSupport.SeedUserAsync(host.Context, emailA);
        await CoreSessionSupport.SeedUserAsync(host.Context, emailB);
        var tokenForA = await AccountManagementSupport.GeneratePasswordResetTokenAsync(host.Context, userA);

        using var client = host.Context.HttpClientFactory();
        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/manage/reset-password",
            new ResetPasswordRequest { Email = emailB, Token = tokenForA, NewPassword = "Correct-Horse-2" }, cancellationToken: cancellationToken);
        var evidence = exchange.ToEvidence("User A's reset token, presented with user B's email address.");

        if (exchange.StatusCode == HttpStatusCode.NoContent)
            return ProbeResult.Failed("User A's reset token was accepted against user B's account - a cross-user password reset.", evidence);

        return ProbeResult.Passed("User A's reset token was rejected when presented with user B's email address.", evidence);
    }
}
