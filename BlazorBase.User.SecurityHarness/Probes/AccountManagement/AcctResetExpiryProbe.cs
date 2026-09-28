using System.Net;
using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.AccountManagement;

/// <summary>
/// Uses a password-reset token after its own lifespan has passed. The framework never exposes a
/// configuration seam for <c>DataProtectionTokenProviderOptions.TokenLifespan</c> (Identity's default
/// is one day), so <see cref="Hosting.HarnessHostFactory"/> binds it from
/// <c>Tokens:DataProtection:TokenLifespan</c> for exactly this probe - a real, if short, wait rather
/// than a <see cref="HarnessTimeProvider"/> advance, since <c>DataProtectorTokenProvider</c> reads the
/// real wall clock, not the DI <see cref="TimeProvider"/>.
/// </summary>
internal sealed class AcctResetExpiryProbe : IAuthProbe
{
    private static readonly TimeSpan TokenLifespan = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan WaitPastExpiry = TimeSpan.FromSeconds(3);

    public string Id => "ACCT-RESET-EXPIRY";
    public string Category => "Account management";
    public string Attack => $"A password-reset token used {WaitPastExpiry.TotalSeconds:F0} real seconds after a {TokenLifespan.TotalSeconds:F0}-second token lifespan.";
    public string Expectation => "Rejected once the token's lifespan has passed.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var overrides = new Dictionary<string, string?> { ["Tokens:DataProtection:TokenLifespan"] = "00:00:02" };
        await using var host = await context.StartIsolatedHostAsync(overrides, cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("reset-expiry");
        var user = await CoreSessionSupport.SeedUserAsync(host.Context, email);
        var token = await AccountManagementSupport.GeneratePasswordResetTokenAsync(host.Context, user);

        await Task.Delay(WaitPastExpiry, cancellationToken);

        using var client = host.Context.HttpClientFactory();
        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/manage/reset-password",
            new ResetPasswordRequest { Email = email, Token = token, NewPassword = "Correct-Horse-2" }, cancellationToken: cancellationToken);
        var evidence = exchange.ToEvidence($"Reset attempt {WaitPastExpiry.TotalSeconds:F0} real seconds after the token was minted with a {TokenLifespan.TotalSeconds:F0}-second lifespan.");

        if (exchange.StatusCode == HttpStatusCode.NoContent)
            return ProbeResult.Failed("The expired reset token still succeeded.", evidence);

        return ProbeResult.Passed("The expired reset token was rejected.", evidence);
    }
}
