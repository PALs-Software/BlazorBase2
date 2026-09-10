using System.Net;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.TwoFactor;

/// <summary>
/// Disabling two-factor rotates the security stamp twice over (<c>SetTwoFactorEnabledAsync</c> and
/// <c>ResetAuthenticatorKeyAsync</c>) - a client that adopts the response's
/// <see cref="TwoFactorDisableResponse.RefreshedTokens"/> the way <c>AuthService</c> now does must
/// still be able to make an authenticated request right afterward.
/// </summary>
internal sealed class TwoFactorDisableKeepsSessionProbe : IAuthProbe
{
    public string Id => "2FA-DISABLE-KEEPS-SESSION";
    public string Category => "Two-factor";
    public string Attack => "Enrols in two-factor, disables it, then makes an authenticated request with the token the disable response refreshed.";
    public string Expectation => "The final authenticated request succeeds; the caller is never signed out by disabling their own two-factor.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();

        var account = await TwoFactorSupport.EnableTwoFactorAsync(context, client, "2fa-disable-keeps-session", cancellationToken);
        var (pendingToken, loginExchange) = await TwoFactorSupport.LoginExpectingPendingAsync(client, context.BaseAddress, account.Email, cancellationToken);

        if (loginExchange.StatusCode != HttpStatusCode.Accepted)
            return ProbeResult.Inconclusive("The account did not answer 202 for the pending-login precondition.", loginExchange.ToEvidence("Login."));

        var validCode = Totp.ComputeCode(account.AuthenticatorSecret, DateTimeOffset.UtcNow);
        var completion = await TwoFactorSupport.CompleteTwoFactorAsync(client, context.BaseAddress, pendingToken, validCode, isRecoveryCode: false, cancellationToken);
        CoreSessionSupport.TryDeserialize<LoginResponse>(completion.ResponseBody, out var session);

        if (completion.StatusCode != HttpStatusCode.OK || session is null)
            return ProbeResult.Inconclusive("Could not complete the second factor to obtain a bearer token for the disable attempt.", completion.ToEvidence("Completion."));

        var disableCode = Totp.ComputeCode(account.AuthenticatorSecret, DateTimeOffset.UtcNow);
        var disableExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, context.BaseAddress, "api/auth/manage/2fa/disable",
            new TwoFactorVerificationRequest { Code = disableCode }, bearerToken: session.AccessToken, cancellationToken: cancellationToken);
        CoreSessionSupport.TryDeserialize<TwoFactorDisableResponse>(disableExchange.ResponseBody, out var disableResult);

        var evidence = new List<ProbeEvidence>
        {
            disableExchange.ToEvidence("Disabled two-factor with the bearer token obtained at login."),
        };

        if (disableExchange.StatusCode != HttpStatusCode.OK)
            return ProbeResult.Failed($"Disabling two-factor did not answer 200: {disableExchange.StatusCode}.", evidence);

        var tokensAfterDisable = disableResult?.RefreshedTokens;

        if (tokensAfterDisable is null)
            return ProbeResult.Failed("Disabling two-factor rotated the security stamp but returned no refreshed token pair.", evidence);

        var authenticatedRequest = await CoreSessionSupport.SendAsync(client, HttpMethod.Get, context.BaseAddress, "api/auth/manage/2fa",
            bearerToken: tokensAfterDisable.AccessToken, cancellationToken: cancellationToken);
        evidence.Add(authenticatedRequest.ToEvidence("An authenticated request right after disabling two-factor."));

        if (authenticatedRequest.StatusCode != HttpStatusCode.OK)
            return ProbeResult.Failed($"The caller's next authenticated request after disabling two-factor was rejected: {authenticatedRequest.StatusCode}.", evidence);

        return ProbeResult.Passed("Disabling two-factor and a following authenticated request both succeeded by adopting the refreshed token pair.", evidence);
    }
}
