using System.Net;
using System.Text.RegularExpressions;
using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.TwoFactor;

/// <summary>Enabling two-factor with a wrong TOTP code must be rejected, and two-factor must stay off.</summary>
internal sealed partial class TwoFactorEnableWrongCodeProbe : IAuthProbe
{
    [GeneratedRegex("secret=([^&]+)")]
    private static partial Regex SecretPattern();

    public string Id => "2FA-ENABLE-WRONG-CODE";
    public string Category => "Two-factor";
    public string Attack => "Calls 2fa/enable with a wrong TOTP code.";
    public string Expectation => "Rejected, and two-factor stays off.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();

        var email = CoreSessionSupport.UniqueEmail("2fa-enable-wrong-code");
        await CoreSessionSupport.SeedUserAsync(context, email);

        var (tokens, loginExchange) = await CoreSessionSupport.LoginAsync(client, context.BaseAddress, email, CoreSessionSupport.SeedPassword, cancellationToken);

        if (tokens is null)
            return ProbeResult.Inconclusive("The initial login (before two-factor is enabled) did not answer 200.", loginExchange.ToEvidence("Login."));

        var statusExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Get, context.BaseAddress, "api/auth/manage/2fa", bearerToken: tokens.AccessToken, cancellationToken: cancellationToken);
        CoreSessionSupport.TryDeserialize<TwoFactorStatusResponse>(statusExchange.ResponseBody, out var status);
        var secretMatch = status?.AuthenticatorUri is { } uri ? SecretPattern().Match(uri) : Match.Empty;

        if (!secretMatch.Success)
            return ProbeResult.Inconclusive("No authenticator secret was returned; the wrong-code attempt could not be built.", statusExchange.ToEvidence("api/auth/manage/2fa."));

        var secret = secretMatch.Groups[1].Value;
        var correctCode = Totp.ComputeCode(secret, DateTimeOffset.UtcNow);
        var wrongCode = ((int.Parse(correctCode) + 1) % 1_000_000).ToString().PadLeft(6, '0');

        var enableAttempt = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, context.BaseAddress, "api/auth/manage/2fa/enable",
            new TwoFactorEnableRequest { Code = wrongCode }, bearerToken: tokens.AccessToken, cancellationToken: cancellationToken);

        var statusAfter = await CoreSessionSupport.SendAsync(client, HttpMethod.Get, context.BaseAddress, "api/auth/manage/2fa", bearerToken: tokens.AccessToken, cancellationToken: cancellationToken);
        CoreSessionSupport.TryDeserialize<TwoFactorStatusResponse>(statusAfter.ResponseBody, out var statusAfterValue);

        var evidence = new List<ProbeEvidence>
        {
            enableAttempt.ToEvidence("2fa/enable with a wrong TOTP code."),
            statusAfter.ToEvidence("Two-factor status after the attempt."),
        };

        var failures = new List<string>();

        if (enableAttempt.StatusCode == HttpStatusCode.OK)
            failures.Add("a wrong code enabled two-factor");

        if (statusAfterValue?.Enabled == true)
            failures.Add("two-factor is enabled after a wrong-code attempt");

        if (failures.Count > 0)
            return ProbeResult.Failed(string.Join("; ", failures) + ".", evidence);

        return ProbeResult.Passed("A wrong code was rejected by 2fa/enable, and two-factor stayed off.", evidence);
    }
}
