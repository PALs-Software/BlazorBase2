using System.Net;
using System.Text.RegularExpressions;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.TwoFactor;

/// <summary>
/// Walks the real enrolment flow a client is expected to follow: log in, read the two-factor setup
/// (which lazily generates the authenticator key and rotates the security stamp), enable two-factor
/// (which rotates it again), then make an authenticated request - adopting each response's
/// <see cref="TwoFactorStatusResponse.RefreshedTokens"/> along the way exactly as
/// <c>AuthService</c> now does. Fails if the caller's session is invalidated anywhere along the way.
/// </summary>
internal sealed partial class TwoFactorSetupKeepsSessionProbe : IAuthProbe
{
    [GeneratedRegex("secret=([^&]+)")]
    private static partial Regex SecretPattern();

    public string Id => "2FA-SETUP-KEEPS-SESSION";
    public string Category => "Two-factor";
    public string Attack => "Logs in, reads the two-factor setup, enables two-factor, then makes an authenticated request - adopting each response's refreshed token pair.";
    public string Expectation => "The final authenticated request succeeds; the caller is never signed out mid-enrolment.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();

        var email = CoreSessionSupport.UniqueEmail("2fa-setup-keeps-session");
        await CoreSessionSupport.SeedUserAsync(context, email);

        var (loginTokens, loginExchange) = await CoreSessionSupport.LoginAsync(client, context.BaseAddress, email, CoreSessionSupport.SeedPassword, cancellationToken);

        if (loginTokens is null)
            return ProbeResult.Inconclusive("Login did not answer 200 for the precondition.", loginExchange.ToEvidence("Login."));

        var statusExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Get, context.BaseAddress, "api/auth/manage/2fa",
            bearerToken: loginTokens.AccessToken, cancellationToken: cancellationToken);
        CoreSessionSupport.TryDeserialize<TwoFactorStatusResponse>(statusExchange.ResponseBody, out var status);

        var secretMatch = status?.AuthenticatorUri is { } uri ? SecretPattern().Match(uri) : Match.Empty;

        if (statusExchange.StatusCode != HttpStatusCode.OK || !secretMatch.Success)
            return ProbeResult.Inconclusive("Reading the two-factor setup did not answer 200 with an authenticator secret.", statusExchange.ToEvidence("Read setup."));

        var secret = secretMatch.Groups[1].Value;
        var tokensAfterSetup = status!.RefreshedTokens;

        if (tokensAfterSetup is null)
            return ProbeResult.Failed("Reading the setup generated the authenticator key (rotating the security stamp) but returned no refreshed token pair - a client following the documented contract has no way to keep its session.",
                statusExchange.ToEvidence("Read setup."));

        var code = Totp.ComputeCode(secret, DateTimeOffset.UtcNow);
        var enableExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, context.BaseAddress, "api/auth/manage/2fa/enable",
            new TwoFactorEnableRequest { Code = code }, bearerToken: tokensAfterSetup.AccessToken, cancellationToken: cancellationToken);
        CoreSessionSupport.TryDeserialize<TwoFactorRecoveryCodesResponse>(enableExchange.ResponseBody, out var enableResult);

        var evidence = new List<ProbeEvidence>
        {
            statusExchange.ToEvidence("Read the two-factor setup."),
            enableExchange.ToEvidence("Enabled two-factor with the refreshed token from the setup response."),
        };

        if (enableExchange.StatusCode != HttpStatusCode.OK)
            return ProbeResult.Failed($"Enabling two-factor with the token the setup response refreshed was rejected: {enableExchange.StatusCode}. The caller was signed out mid-enrolment.", evidence);

        var tokensAfterEnable = enableResult?.RefreshedTokens;

        if (tokensAfterEnable is null)
            return ProbeResult.Failed("Enabling two-factor rotated the security stamp again but returned no refreshed token pair.", evidence);

        var authenticatedRequest = await CoreSessionSupport.SendAsync(client, HttpMethod.Get, context.BaseAddress, "api/auth/manage/2fa",
            bearerToken: tokensAfterEnable.AccessToken, cancellationToken: cancellationToken);
        evidence.Add(authenticatedRequest.ToEvidence("An authenticated request right after enabling two-factor."));

        if (authenticatedRequest.StatusCode != HttpStatusCode.OK)
            return ProbeResult.Failed($"The caller's next authenticated request after enabling two-factor was rejected: {authenticatedRequest.StatusCode}.", evidence);

        return ProbeResult.Passed("Login, reading the setup, enabling two-factor and a following authenticated request all succeeded by adopting each response's refreshed token pair.", evidence);
    }
}
