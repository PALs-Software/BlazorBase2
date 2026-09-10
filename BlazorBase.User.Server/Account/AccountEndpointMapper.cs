using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Configuration;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Tokens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.Account;

/// <summary>
/// Maps the account lifecycle Identity's token providers already support but the framework never
/// exposed: email confirmation, self-service password reset, and TOTP two-factor enrolment. Built on
/// <c>UserManager</c>'s own token generation and verification rather than any hand-rolled equivalent.
/// </summary>
public static class AccountEndpointMapper
{
    /// <summary>
    /// Log category for the handlers below. Spelled out because a static class cannot be used as the
    /// type argument of <c>ILogger&lt;T&gt;</c>, and the user entity would be a misleading category.
    /// </summary>
    private const string LogCategory = "BlazorBase.User.Server.Account.AccountEndpointMapper";

    /// <summary>
    /// Maps every account-management endpoint under <paramref name="path"/>, all rate-limited by the
    /// "BlazorBaseAuth" policy registered by <c>AddBlazorBaseUserServer</c>. <c>confirm-email</c>,
    /// <c>resend-confirmation</c>, <c>forgot-password</c> and <c>reset-password</c> are anonymous;
    /// every <c>2fa</c> endpoint requires an authenticated caller.
    /// </summary>
    public static IEndpointRouteBuilder MapBlazorBaseAccountEndpoints<TUser>(
        this IEndpointRouteBuilder endpoints,
        string path = "api/auth/manage")
        where TUser : BaseUser, new()
    {
        var group = endpoints.MapGroup(path).RequireRateLimiting("BlazorBaseAuth");

        group.MapPost("confirm-email", ConfirmEmailAsync<TUser>).AllowAnonymous();
        group.MapPost("resend-confirmation", ResendConfirmationAsync<TUser>).AllowAnonymous();
        group.MapPost("forgot-password", ForgotPasswordAsync<TUser>).AllowAnonymous();
        group.MapPost("reset-password", ResetPasswordAsync<TUser>).AllowAnonymous();

        group.MapGet("2fa", GetTwoFactorStatusAsync<TUser>).RequireAuthorization();
        group.MapPost("2fa/enable", EnableTwoFactorAsync<TUser>).RequireAuthorization();
        group.MapPost("2fa/disable", DisableTwoFactorAsync<TUser>).RequireAuthorization();
        group.MapPost("2fa/recovery-codes", RegenerateRecoveryCodesAsync<TUser>).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> ConfirmEmailAsync<TUser>(ConfirmEmailRequest request, UserManager<TUser> userManager)
        where TUser : BaseUser
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null)
            return Results.BadRequest(new { message = "Invalid confirmation link." });

        var result = await userManager.ConfirmEmailAsync(user, request.Token);

        return result.Succeeded ? Results.Ok() : Results.BadRequest(new { message = "Invalid confirmation link." });
    }

    private static async Task<IResult> ResendConfirmationAsync<TUser>(
        ResendConfirmationRequest request,
        UserManager<TUser> userManager,
        IEmailSender<TUser> emailSender,
        IOptions<AccountManagementOptions> options,
        ILoggerFactory loggerFactory)
        where TUser : BaseUser
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            var user = await userManager.FindByEmailAsync(request.Email);
            if (user is not null && !await userManager.IsEmailConfirmedAsync(user))
            {
                var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
                var link = BuildLink(options.Value.ClientBaseUrl, "confirm-email", ("userId", user.Id), ("token", token));
                await emailSender.SendConfirmationLinkAsync(user, request.Email, link);
            }
        }
        catch (Exception exception)
        {
            // The address is deliberately not logged: it is unauthenticated caller input, and an
            // enumeration attempt would otherwise write every probed address into the log.
            loggerFactory.CreateLogger(LogCategory).LogError(exception, "resend-confirmation failed behind the response.");
        }

        // Identical regardless of whether the address is known, already confirmed or the send behind
        // it failed - anything else is an account-enumeration oracle. The timing floor below closes
        // the same oracle for latency: only the branch above sends mail, and a network round trip is
        // otherwise measurable.
        await PadResponseToTimingFloorAsync(startTimestamp, options.Value);

        return Results.Accepted();
    }

    private static async Task<IResult> ForgotPasswordAsync<TUser>(
        ForgotPasswordRequest request,
        UserManager<TUser> userManager,
        IEmailSender<TUser> emailSender,
        IOptions<AccountManagementOptions> options,
        ILoggerFactory loggerFactory)
        where TUser : BaseUser
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            var user = await userManager.FindByEmailAsync(request.Email);
            if (user is not null)
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                var link = BuildLink(options.Value.ClientBaseUrl, "reset-password", ("email", request.Email), ("token", token));
                await emailSender.SendPasswordResetLinkAsync(user, request.Email, link);
            }
        }
        catch (Exception exception)
        {
            // The address is deliberately not logged: it is unauthenticated caller input, and an
            // enumeration attempt would otherwise write every probed address into the log.
            loggerFactory.CreateLogger(LogCategory).LogError(exception, "forgot-password failed behind the response.");
        }

        // Identical regardless of whether the address is known or the send behind it failed - a
        // distinguishable response would be an account-enumeration oracle. The timing floor below
        // closes the same oracle for latency: only the branch above sends mail, and a network round
        // trip is otherwise measurable.
        await PadResponseToTimingFloorAsync(startTimestamp, options.Value);

        return Results.Accepted();
    }

    /// <summary>
    /// Delays the response until <paramref name="startTimestamp"/> plus a randomised target between
    /// <see cref="AccountManagementOptions.TimingFloorMinimumMilliseconds"/> and
    /// <see cref="AccountManagementOptions.TimingFloorMaximumMilliseconds"/> have elapsed, so the
    /// existing- and unknown-address branches of <c>forgot-password</c> and <c>resend-confirmation</c>
    /// become indistinguishable by response time, not just by response body. The target is drawn from
    /// <see cref="RandomNumberGenerator"/> rather than <see cref="Random.Shared"/> because a
    /// predictable pad could be averaged out and subtracted back by an attacker sampling many
    /// requests. A configured maximum below the minimum clamps up to the minimum instead of throwing,
    /// so a bad configuration value degrades to a fixed floor rather than crashing the endpoint. With
    /// the default background mail dispatch the send itself never runs on this path, so the floor only
    /// needs to cover the user lookup and token generation that remain here; a host that switches to
    /// <see cref="BlazorBase.Mailing.Models.EmailDispatchMode.Inline"/> puts the send back on the
    /// response path and must raise these values to cover it again.
    /// </summary>
    private static async Task PadResponseToTimingFloorAsync(long startTimestamp, AccountManagementOptions options)
    {
        var minimumMilliseconds = Math.Max(options.TimingFloorMinimumMilliseconds, 0);
        var maximumMilliseconds = Math.Max(options.TimingFloorMaximumMilliseconds, minimumMilliseconds);

        var targetMilliseconds = maximumMilliseconds > minimumMilliseconds
            ? RandomNumberGenerator.GetInt32(minimumMilliseconds, maximumMilliseconds)
            : minimumMilliseconds;

        var remainingMilliseconds = targetMilliseconds - Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        if (remainingMilliseconds > 0)
            await Task.Delay((int)remainingMilliseconds);
    }

    /// <summary>
    /// Reports failure honestly, unlike <c>forgot-password</c>. The reset token is itself the
    /// credential, so saying it is invalid tells the caller only something about the token already
    /// in their hands - it reveals no account. Staying silent instead would answer an expired link
    /// with success and leave the password unchanged, and swallow a rejected password outright.
    /// </summary>
    private static async Task<IResult> ResetPasswordAsync<TUser>(ResetPasswordRequest request, UserManager<TUser> userManager)
        where TUser : BaseUser
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        var result = user is null
            ? IdentityResult.Failed(new IdentityError { Description = "Invalid or expired password reset link." })
            : await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);

        if (!result.Succeeded)
            return Results.BadRequest(new { errors = result.Errors.Select(error => error.Description) });

        return Results.NoContent();
    }

    private static async Task<IResult> GetTwoFactorStatusAsync<TUser>(
        HttpContext httpContext,
        UserManager<TUser> userManager,
        SignInManager<TUser> signInManager,
        AuthTokenIssuer<TUser> tokenIssuer,
        IAuthTokenTransport tokenTransport,
        IOptions<AccountManagementOptions> options,
        CancellationToken cancellationToken)
        where TUser : BaseUser
    {
        var user = await ResolveCurrentUserAsync(httpContext, userManager);
        if (user is null)
            return Results.Unauthorized();

        var enabled = await userManager.GetTwoFactorEnabledAsync(user);
        var recoveryCodesRemaining = await userManager.CountRecoveryCodesAsync(user);

        string? authenticatorUri = null;
        LoginResponse? refreshedTokens = null;

        if (!enabled)
        {
            var (uri, keyWasJustCreated) = await ResolveAuthenticatorUriAsync(user, userManager, options.Value.Issuer);
            authenticatorUri = uri;

            if (keyWasJustCreated)
                refreshedTokens = await RefreshSessionAsync(httpContext, user, signInManager, tokenIssuer, tokenTransport, cancellationToken);
        }

        return Results.Ok(new TwoFactorStatusResponse
        {
            Enabled = enabled,
            RecoveryCodesRemaining = recoveryCodesRemaining,
            AuthenticatorUri = authenticatorUri,
            RefreshedTokens = refreshedTokens,
        });
    }

    private static async Task<IResult> EnableTwoFactorAsync<TUser>(
        TwoFactorEnableRequest request,
        HttpContext httpContext,
        UserManager<TUser> userManager,
        SignInManager<TUser> signInManager,
        AuthTokenIssuer<TUser> tokenIssuer,
        IAuthTokenTransport tokenTransport,
        CancellationToken cancellationToken)
        where TUser : BaseUser
    {
        var user = await ResolveCurrentUserAsync(httpContext, userManager);
        if (user is null)
            return Results.Unauthorized();

        if (!await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, request.Code))
            return Results.BadRequest(new { message = "Invalid code." });

        await userManager.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        var refreshedTokens = await RefreshSessionAsync(httpContext, user, signInManager, tokenIssuer, tokenTransport, cancellationToken);

        return Results.Ok(new TwoFactorRecoveryCodesResponse { RecoveryCodes = [.. recoveryCodes ?? []], RefreshedTokens = refreshedTokens });
    }

    private static async Task<IResult> DisableTwoFactorAsync<TUser>(
        TwoFactorVerificationRequest request,
        HttpContext httpContext,
        UserManager<TUser> userManager,
        SignInManager<TUser> signInManager,
        AuthTokenIssuer<TUser> tokenIssuer,
        IAuthTokenTransport tokenTransport,
        CancellationToken cancellationToken)
        where TUser : BaseUser
    {
        var user = await ResolveCurrentUserAsync(httpContext, userManager);
        if (user is null)
            return Results.Unauthorized();

        if (!await userManager.GetTwoFactorEnabledAsync(user))
            return Results.BadRequest(new { message = "Two-factor authentication is not enabled." });

        if (!await VerifyTwoFactorProofAsync(user, request.Code, userManager))
            return Results.BadRequest(new { message = "Invalid code." });

        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        var refreshedTokens = await RefreshSessionAsync(httpContext, user, signInManager, tokenIssuer, tokenTransport, cancellationToken);

        return Results.Ok(new TwoFactorDisableResponse { RefreshedTokens = refreshedTokens });
    }

    /// <summary>
    /// Mints a fresh token pair for <paramref name="user"/> and delivers it through the configured
    /// <see cref="IAuthTokenTransport"/>, exactly as <c>AuthControllerBase.GenerateTokenResponse</c>
    /// does at login - needed after any operation on this endpoint group that rotates the security
    /// stamp, so the caller's own session survives an operation it performed on itself. Under the
    /// cookie transport this rewrites the caller's session cookie and returns a pair with both token
    /// strings blanked; under the bearer transport it returns the real pair for the caller to adopt.
    /// </summary>
    private static async Task<LoginResponse> RefreshSessionAsync<TUser>(
        HttpContext httpContext,
        TUser user,
        SignInManager<TUser> signInManager,
        AuthTokenIssuer<TUser> tokenIssuer,
        IAuthTokenTransport tokenTransport,
        CancellationToken cancellationToken)
        where TUser : BaseUser
    {
        var principal = await signInManager.CreateUserPrincipalAsync(user);
        var tokens = await tokenIssuer.IssueTokenPairAsync(principal, cancellationToken: cancellationToken);
        return await tokenTransport.WriteAsync(httpContext, tokens, principal, cancellationToken);
    }

    private static async Task<IResult> RegenerateRecoveryCodesAsync<TUser>(
        TwoFactorVerificationRequest request,
        HttpContext httpContext,
        UserManager<TUser> userManager)
        where TUser : BaseUser
    {
        var user = await ResolveCurrentUserAsync(httpContext, userManager);
        if (user is null)
            return Results.Unauthorized();

        if (!await userManager.GetTwoFactorEnabledAsync(user))
            return Results.BadRequest(new { message = "Two-factor authentication is not enabled." });

        if (!await VerifyTwoFactorProofAsync(user, request.Code, userManager))
            return Results.BadRequest(new { message = "Invalid code." });

        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

        return Results.Ok(new TwoFactorRecoveryCodesResponse { RecoveryCodes = [.. recoveryCodes ?? []] });
    }

    /// <summary>
    /// A TOTP check is non-destructive and tried first; falling back to a recovery code only on
    /// failure means an account that has lost the authenticator can still prove possession, and the
    /// code it spends to do so is consumed - it is the same single-use credential either way.
    /// </summary>
    private static async Task<bool> VerifyTwoFactorProofAsync<TUser>(TUser user, string code, UserManager<TUser> userManager)
        where TUser : BaseUser
    {
        if (string.IsNullOrEmpty(code))
            return false;

        if (await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code))
            return true;

        var result = await userManager.RedeemTwoFactorRecoveryCodeAsync(user, code);
        return result.Succeeded;
    }

    private static async Task<TUser?> ResolveCurrentUserAsync<TUser>(HttpContext httpContext, UserManager<TUser> userManager)
        where TUser : BaseUser
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrEmpty(userId) ? null : await userManager.FindByIdAsync(userId);
    }

    /// <summary>
    /// Reports <paramref name="KeyWasJustCreated"/> as well as the URI, since generating a key for the
    /// first time rotates the security stamp and the caller needs to know whether that happened.
    /// </summary>
    private static async Task<(string Uri, bool KeyWasJustCreated)> ResolveAuthenticatorUriAsync<TUser>(TUser user, UserManager<TUser> userManager, string issuer)
        where TUser : BaseUser
    {
        var key = await userManager.GetAuthenticatorKeyAsync(user);
        var keyWasJustCreated = string.IsNullOrEmpty(key);

        if (keyWasJustCreated)
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            key = await userManager.GetAuthenticatorKeyAsync(user);
        }

        var accountName = user.Email ?? user.UserName ?? user.Id;

        var uri = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(accountName)}" +
               $"?secret={key}&issuer={Uri.EscapeDataString(issuer)}&digits=6";

        return (uri, keyWasJustCreated);
    }

    private static string BuildLink(string? clientBaseUrl, string relativePath, params (string Key, string Value)[] queryParameters)
    {
        var query = string.Join('&', queryParameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        return $"{clientBaseUrl?.TrimEnd('/')}/{relativePath}?{query}";
    }
}
