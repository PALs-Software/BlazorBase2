using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Configuration;
using BlazorBase.User.Server.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
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
        IOptions<AccountManagementOptions> options)
        where TUser : BaseUser
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is not null && !await userManager.IsEmailConfirmedAsync(user))
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var link = BuildLink(options.Value.ClientBaseUrl, "confirm-email", ("userId", user.Id), ("token", token));
            await emailSender.SendConfirmationLinkAsync(user, request.Email, link);
        }

        // Identical regardless of whether the address is known or already confirmed - anything else
        // is an account-enumeration oracle. The timing floor below closes the same oracle for latency:
        // only the branch above sends mail, and a network round trip is otherwise measurable.
        await PadResponseToTimingFloorAsync(startTimestamp, options.Value);

        return Results.Accepted();
    }

    private static async Task<IResult> ForgotPasswordAsync<TUser>(
        ForgotPasswordRequest request,
        UserManager<TUser> userManager,
        IEmailSender<TUser> emailSender,
        IOptions<AccountManagementOptions> options)
        where TUser : BaseUser
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is not null)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var link = BuildLink(options.Value.ClientBaseUrl, "reset-password", ("email", request.Email), ("token", token));
            await emailSender.SendPasswordResetLinkAsync(user, request.Email, link);
        }

        // Identical regardless of whether the address is known - a distinguishable response would be
        // an account-enumeration oracle. The timing floor below closes the same oracle for latency:
        // only the branch above sends mail, and a network round trip is otherwise measurable.
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
    /// so a bad configuration value degrades to a fixed floor rather than crashing the endpoint. A
    /// mail send slower than the drawn floor still shows through it - the remedy is for the host to
    /// raise these values, not to close this gap in code.
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
        IOptions<AccountManagementOptions> options)
        where TUser : BaseUser
    {
        var user = await ResolveCurrentUserAsync(httpContext, userManager);
        if (user is null)
            return Results.Unauthorized();

        var enabled = await userManager.GetTwoFactorEnabledAsync(user);
        var recoveryCodesRemaining = await userManager.CountRecoveryCodesAsync(user);

        return Results.Ok(new TwoFactorStatusResponse
        {
            Enabled = enabled,
            RecoveryCodesRemaining = recoveryCodesRemaining,
            AuthenticatorUri = enabled ? null : await ResolveAuthenticatorUriAsync(user, userManager, options.Value.Issuer),
        });
    }

    private static async Task<IResult> EnableTwoFactorAsync<TUser>(
        TwoFactorEnableRequest request,
        HttpContext httpContext,
        UserManager<TUser> userManager)
        where TUser : BaseUser
    {
        var user = await ResolveCurrentUserAsync(httpContext, userManager);
        if (user is null)
            return Results.Unauthorized();

        if (!await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, request.Code))
            return Results.BadRequest(new { message = "Invalid code." });

        await userManager.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

        return Results.Ok(new TwoFactorRecoveryCodesResponse { RecoveryCodes = [.. recoveryCodes ?? []] });
    }

    private static async Task<IResult> DisableTwoFactorAsync<TUser>(
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

        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);

        return Results.NoContent();
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

    private static async Task<string> ResolveAuthenticatorUriAsync<TUser>(TUser user, UserManager<TUser> userManager, string issuer)
        where TUser : BaseUser
    {
        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            key = await userManager.GetAuthenticatorKeyAsync(user);
        }

        var accountName = user.Email ?? user.UserName ?? user.Id;

        return $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(accountName)}" +
               $"?secret={key}&issuer={Uri.EscapeDataString(issuer)}&digits=6";
    }

    private static string BuildLink(string? clientBaseUrl, string relativePath, params (string Key, string Value)[] queryParameters)
    {
        var query = string.Join('&', queryParameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        return $"{clientBaseUrl?.TrimEnd('/')}/{relativePath}?{query}";
    }
}
