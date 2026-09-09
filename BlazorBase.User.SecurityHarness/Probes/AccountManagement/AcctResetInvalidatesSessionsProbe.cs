using System.Net;
using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.AccountManagement;

/// <summary>
/// The probe the plan built this harness around: after a password reset, does an attacker's own
/// still-live refresh token and access token keep working? A password reset is what a user does after
/// believing they are compromised - anything of the attacker's that survives it means the reset
/// achieved nothing for that credential. Reports exactly what survived and for how long, rather than
/// a verdict rounded to "safe".
/// </summary>
internal sealed class AcctResetInvalidatesSessionsProbe : IAuthProbe
{
    private const int AccessTokenMinutes = 1;

    public string Id => "ACCT-RESET-INVALIDATES-SESSIONS";
    public string Category => "Account management";
    public string Attack => "Resets the account's password, then presents a refresh token and an access token both issued before the reset.";
    public string Expectation => "Neither survives - a password reset is what a user does after believing they are compromised.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var overrides = new Dictionary<string, string?> { ["AuthTokens:AccessTokenMinutes"] = AccessTokenMinutes.ToString() };
        await using var host = await context.StartIsolatedHostAsync(overrides, cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("reset-invalidates");
        var user = await CoreSessionSupport.SeedUserAsync(host.Context, email);

        using var client = host.Context.HttpClientFactory();
        var (tokens, loginExchange) = await CoreSessionSupport.LoginAsync(client, host.Context.BaseAddress, email, CoreSessionSupport.SeedPassword, cancellationToken);

        if (tokens is null)
            return ProbeResult.Inconclusive("Login did not return a token pair.", loginExchange.ToEvidence("Login."));

        var resetToken = await AccountManagementSupport.GeneratePasswordResetTokenAsync(host.Context, user);
        var resetExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/manage/reset-password",
            new ResetPasswordRequest { Email = email, Token = resetToken, NewPassword = "Correct-Horse-2" }, cancellationToken: cancellationToken);

        if (resetExchange.StatusCode != HttpStatusCode.NoContent)
            return ProbeResult.Inconclusive($"The password reset itself did not succeed (got {(int)resetExchange.StatusCode}); session survival cannot be measured.", resetExchange.ToEvidence("Password reset."));

        var refreshAfterReset = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/refresh",
            new RefreshTokenRequest { RefreshToken = tokens.RefreshToken }, cancellationToken: cancellationToken);

        var accessImmediatelyAfterReset = await CoreSessionSupport.CallProtectedEndpointAsync(client, host.Context.BaseAddress, tokens.AccessToken, cancellationToken);

        var harnessTimeProvider = (HarnessTimeProvider)host.Context.TimeProvider;
        harnessTimeProvider.Advance(TimeSpan.FromMinutes(AccessTokenMinutes) + TimeSpan.FromSeconds(5));

        var accessAfterNaturalExpiry = await CoreSessionSupport.CallProtectedEndpointAsync(client, host.Context.BaseAddress, tokens.AccessToken, cancellationToken);

        var evidence = new[]
        {
            resetExchange.ToEvidence("The password reset."),
            refreshAfterReset.ToEvidence("The pre-reset refresh token, presented immediately after the reset."),
            accessImmediatelyAfterReset.ToEvidence("The pre-reset access token, presented immediately after the reset."),
            accessAfterNaturalExpiry.ToEvidence($"The same pre-reset access token, after advancing the harness TimeProvider by its full {AccessTokenMinutes}-minute lifetime plus 5 seconds."),
        };

        var refreshSurvived = refreshAfterReset.StatusCode == HttpStatusCode.OK;
        var accessSurvivedImmediately = accessImmediatelyAfterReset.StatusCode == HttpStatusCode.OK;
        var accessSurvivedToNaturalExpiry = accessAfterNaturalExpiry.StatusCode != HttpStatusCode.Unauthorized;

        var summary =
            $"Refresh token after reset: {(refreshSurvived ? "STILL WORKS" : "rejected")} ({(int)refreshAfterReset.StatusCode}). " +
            $"Access token immediately after reset: {(accessSurvivedImmediately ? "STILL WORKS" : "rejected")} ({(int)accessImmediatelyAfterReset.StatusCode}). " +
            $"Access token after its full {AccessTokenMinutes}-minute lifetime: {(accessSurvivedToNaturalExpiry ? "STILL WORKS" : "rejected, at its natural expiry")} ({(int)accessAfterNaturalExpiry.StatusCode}).";

        if (refreshSurvived || accessSurvivedImmediately)
            return ProbeResult.Failed(summary, evidence);

        return ProbeResult.Passed(summary, evidence);
    }
}
