using System.Net;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.TwoFactor;

/// <summary>
/// Sends wrong TOTP codes against a fresh pending login, on a host with a lowered
/// <c>MaxFailedAccessAttempts</c> so the lockout threshold is reached quickly. Reports whether the
/// rejection message changes once the account locks out (proof the attempts counted toward lockout),
/// and whether any attempt was rate limited - a measurement, not a threshold-based verdict.
/// </summary>
internal sealed class TwoFactorCodeBruteForceProbe : IAuthProbe
{
    private const int MaxFailedAccessAttempts = 3;
    private const int Attempts = MaxFailedAccessAttempts + 2;

    public string Id => "2FA-CODE-BRUTE-FORCE";
    public string Category => "Two-factor";
    public string Attack => $"{Attempts} wrong TOTP codes against one pending login, on a host configured with MaxFailedAccessAttempts={MaxFailedAccessAttempts}.";
    public string Expectation => "Report whether the attempts are rate limited, whether they count towards lockout, and how many a single window allows.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var overrides = new Dictionary<string, string?>
        {
            ["AuthTokens:MaxFailedAccessAttempts"] = MaxFailedAccessAttempts.ToString(),
            ["AuthTokens:RateLimit:PermitLimit"] = "1000",
        };
        await using var host = await context.StartIsolatedHostAsync(overrides, cancellationToken);
        context = host.Context;
        using var client = context.HttpClientFactory();

        var account = await TwoFactorSupport.EnableTwoFactorAsync(context, client, "2fa-brute-force", cancellationToken);
        var (pendingToken, loginExchange) = await TwoFactorSupport.LoginExpectingPendingAsync(client, context.BaseAddress, account.Email, cancellationToken);

        if (loginExchange.StatusCode != HttpStatusCode.Accepted || string.IsNullOrEmpty(pendingToken))
            return ProbeResult.Inconclusive("The initial login did not answer 202 with a pending token; brute force could not be tested.", loginExchange.ToEvidence("Initial login."));

        var evidence = new List<ProbeEvidence>();
        var messages = new List<string>();
        var sawRateLimited = false;

        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            var wrongCode = ((int.Parse(Totp.ComputeCode(account.AuthenticatorSecret, DateTimeOffset.UtcNow)) + 1) % 1_000_000).ToString().PadLeft(6, '0');
            var exchange = await TwoFactorSupport.CompleteTwoFactorAsync(client, context.BaseAddress, pendingToken, wrongCode, isRecoveryCode: false, cancellationToken);
            evidence.Add(exchange.ToEvidence($"Wrong-code attempt {attempt} of {Attempts}."));
            messages.Add(exchange.ResponseBody);

            if (exchange.StatusCode == HttpStatusCode.TooManyRequests)
                sawRateLimited = true;
        }

        var distinctMessages = messages.Distinct().ToList();
        var countedTowardLockout = distinctMessages.Count > 1;

        var summary =
            $"{Attempts} wrong codes sent, MaxFailedAccessAttempts={MaxFailedAccessAttempts}. " +
            $"Rate limited: {sawRateLimited}. Response body changed partway through (evidence the account locked out): {countedTowardLockout}. " +
            $"Distinct response bodies observed: {distinctMessages.Count}.";

        return ProbeResult.Inconclusive(summary, evidence);
    }
}
