using System.Net;
using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.AccountManagement;

/// <summary>
/// Resets a password to an 11-character value that otherwise satisfies every other rule (digit,
/// upper, lower). <see cref="ResetPasswordRequest"/> only requires 8 characters at the model-binding
/// level, so this exercises <see cref="Microsoft.AspNetCore.Identity.PasswordOptions.RequiredLength"/>
/// (12, configured by <c>AddBlazorBaseUserServer</c>) rather than the request's own validation.
/// </summary>
internal sealed class AcctResetWeakPasswordProbe : IAuthProbe
{
    public string Id => "ACCT-RESET-WEAK-PASSWORD";
    public string Category => "Account management";
    public string Attack => "Resets a password to an 11-character value that satisfies every other complexity rule.";
    public string Expectation => "Rejected - the password policy's required length is 12.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("reset-weak-password");
        var user = await CoreSessionSupport.SeedUserAsync(host.Context, email);
        var token = await AccountManagementSupport.GeneratePasswordResetTokenAsync(host.Context, user);

        using var client = host.Context.HttpClientFactory();
        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/manage/reset-password",
            new ResetPasswordRequest { Email = email, Token = token, NewPassword = "Aa1aaaaaaaa" }, cancellationToken: cancellationToken);
        var evidence = exchange.ToEvidence("Reset to an 11-character password satisfying digit/upper/lower.");

        if (exchange.StatusCode == HttpStatusCode.NoContent)
            return ProbeResult.Failed("An 11-character password was accepted despite the configured 12-character minimum.", evidence);

        return ProbeResult.Passed("The 11-character password was rejected.", evidence);
    }
}
