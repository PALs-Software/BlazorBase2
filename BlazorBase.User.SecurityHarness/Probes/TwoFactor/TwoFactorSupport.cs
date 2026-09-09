using System.Net;
using System.Text.RegularExpressions;
using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.TwoFactor;

/// <summary>
/// Shared plumbing every two-factor probe uses: enrolling a fresh account through the real HTTP
/// enrolment flow (seed, log in, read the authenticator secret, compute a genuine TOTP code with
/// <see cref="Totp"/>, enable) rather than writing the enabled flag directly through
/// <c>UserManager</c> - a probe attacking <c>login/2fa</c> should start from an account that reached
/// two-factor the same way a real user would.
/// </summary>
internal static partial class TwoFactorSupport
{
    [GeneratedRegex("secret=([^&]+)")]
    private static partial Regex SecretPattern();

    public static async Task<TwoFactorAccount> EnableTwoFactorAsync(ProbeContext context, HttpClient client, string emailLocalPart, CancellationToken cancellationToken)
    {
        var email = CoreSessionSupport.UniqueEmail(emailLocalPart);
        await CoreSessionSupport.SeedUserAsync(context, email);

        var (tokens, loginExchange) = await CoreSessionSupport.LoginAsync(client, context.BaseAddress, email, CoreSessionSupport.SeedPassword, cancellationToken);

        if (tokens is null)
            throw new InvalidOperationException($"The initial login before enabling two-factor did not answer 200: {loginExchange.StatusCode}.");

        var statusExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Get, context.BaseAddress, "api/auth/manage/2fa", bearerToken: tokens.AccessToken, cancellationToken: cancellationToken);
        CoreSessionSupport.TryDeserialize<TwoFactorStatusResponse>(statusExchange.ResponseBody, out var status);

        var secretMatch = status?.AuthenticatorUri is { } uri ? SecretPattern().Match(uri) : Match.Empty;

        if (!secretMatch.Success)
            throw new InvalidOperationException($"No authenticator secret was returned by api/auth/manage/2fa: {statusExchange.ResponseBody}");

        var secret = secretMatch.Groups[1].Value;
        var code = Totp.ComputeCode(secret, DateTimeOffset.UtcNow);

        var enableExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, context.BaseAddress, "api/auth/manage/2fa/enable",
            new TwoFactorEnableRequest { Code = code }, bearerToken: tokens.AccessToken, cancellationToken: cancellationToken);

        if (enableExchange.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"Enabling two-factor did not answer 200: {enableExchange.StatusCode} {enableExchange.ResponseBody}");

        CoreSessionSupport.TryDeserialize<TwoFactorRecoveryCodesResponse>(enableExchange.ResponseBody, out var recoveryCodes);

        return new TwoFactorAccount(email, secret, recoveryCodes?.RecoveryCodes ?? []);
    }

    /// <summary>Logs in with the account's password, expecting the 202-pending response, and returns its pending token.</summary>
    public static async Task<(string PendingToken, ProbeExchange Exchange)> LoginExpectingPendingAsync(HttpClient client, Uri baseAddress, string email, CancellationToken cancellationToken)
    {
        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, baseAddress, "api/auth/login",
            new LoginRequest { Email = email, Password = CoreSessionSupport.SeedPassword }, cancellationToken: cancellationToken);

        CoreSessionSupport.TryDeserialize<TwoFactorRequiredResponse>(exchange.ResponseBody, out var pending);

        return (pending?.PendingToken ?? string.Empty, exchange);
    }

    public static Task<ProbeExchange> CompleteTwoFactorAsync(HttpClient client, Uri baseAddress, string pendingToken, string code, bool isRecoveryCode, CancellationToken cancellationToken) =>
        CoreSessionSupport.SendAsync(client, HttpMethod.Post, baseAddress, "api/auth/login/2fa",
            new TwoFactorLoginRequest { PendingToken = pendingToken, Code = code, IsRecoveryCode = isRecoveryCode }, cancellationToken: cancellationToken);
}

/// <summary>A two-factor-enrolled account's identity, its authenticator secret (for computing further codes), and its one-time recovery codes.</summary>
internal sealed record TwoFactorAccount(string Email, string AuthenticatorSecret, IReadOnlyList<string> RecoveryCodes);
