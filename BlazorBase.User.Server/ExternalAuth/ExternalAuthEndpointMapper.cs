using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Tokens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BlazorBase.User.Server.ExternalAuth;

/// <summary>
/// Maps the external-token exchange endpoint as a minimal API - the framework's CRUD surface
/// (<c>MapBaseEndpoints</c>, <c>MapBlazorBaseCrudEndpoints</c>) already is minimal API, and a host
/// with no MVC stack at all must not be forced to add one just to reach this feature.
/// </summary>
public static class ExternalAuthEndpointMapper
{
    /// <summary>
    /// The clock skew <see cref="OidcExternalTokenValidator"/> allows past a token's own <c>exp</c>
    /// - kept in sync with that validator's own <c>ClockSkew</c> so a token cannot be replayed in
    /// the window where it is expired by its own claim but still accepted there.
    /// </summary>
    private static readonly TimeSpan TokenValidatorClockSkew = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maps <c>POST {path}/exchange</c>, rate-limited by the "BlazorBaseAuth" policy registered by
    /// <c>AddBlazorBaseUserServer</c>. Validates the external token via the registered
    /// <see cref="IExternalTokenValidator"/>, rejects a token already exchanged before via the
    /// registered <see cref="IExternalTokenReplayCache"/> unless
    /// <see cref="ExternalAuthSettings.ReplayProtectionEnabled"/> is turned off, resolves the user
    /// via the registered <see cref="IExternalUserResolver{TUser}"/>, and returns the same
    /// <c>LoginResponse</c> shape the interactive login/setup/refresh endpoints return.
    /// </summary>
    /// <remarks>
    /// Exempt from the CSRF middleware. The request carries its own credential in the body, and a
    /// cross-site page cannot obtain a provider token for the visitor to put there. Requiring an
    /// antiforgery token instead would lock out the native clients this endpoint exists for, which
    /// have no session to have fetched one from.
    /// </remarks>
    public static IEndpointRouteBuilder MapBlazorBaseExternalAuthEndpoints<TUser>(
        this IEndpointRouteBuilder endpoints,
        string path = "api/auth/external")
        where TUser : BaseUser, new()
    {
        endpoints.MapPost($"{path}/exchange", async (
                HttpContext httpContext,
                ExternalTokenExchangeRequest request,
                IExternalTokenValidator tokenValidator,
                IExternalTokenReplayCache replayCache,
                IOptions<ExternalAuthSettings> settings,
                IExternalUserResolver<TUser> userResolver,
                SignInManager<TUser> signInManager,
                AuthTokenIssuer<TUser> tokenIssuer,
                IAuthTokenTransport tokenTransport,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.ExternalToken))
                    return Results.Unauthorized();

                var externalPrincipal = await tokenValidator.ValidateAsync(request.ExternalToken, cancellationToken);
                if (externalPrincipal is null)
                    return Results.Unauthorized();

                if (settings.Value.ReplayProtectionEnabled && !await IsFirstPresentationAsync(replayCache, request.ExternalToken, externalPrincipal, cancellationToken))
                    return Results.Unauthorized();

                var resolution = await userResolver.ResolveAsync(externalPrincipal, cancellationToken);
                if (!resolution.IsAccepted || resolution.User is null)
                    return Results.Unauthorized();

                if (!await IsAccountActiveAndNotLockedOutAsync(signInManager, resolution.User))
                    return Results.Unauthorized();

                var principal = await signInManager.CreateUserPrincipalAsync(resolution.User);
                var tokens = await tokenIssuer.IssueTokenPairAsync(principal, cancellationToken: cancellationToken);
                var response = await tokenTransport.WriteAsync(httpContext, tokens, principal, cancellationToken);

                return Results.Ok(response);
            })
            .AllowAnonymous()
            .AllowBlazorBaseCsrf()
            .RequireRateLimiting("BlazorBaseAuth");

        return endpoints;
    }

    /// <summary>
    /// Mirrors the account-state gate <see cref="BlazorBase.User.Server.Controllers.AuthControllerBase"/>
    /// applies to every password-based sign-in path - a valid external token alone must not grant a
    /// session to a deactivated or locked-out local account. Shared with
    /// <see cref="ExternalAuthRedirectEndpointMapper"/>, the redirect-based half of external
    /// authentication, so the rule exists in one place.
    /// </summary>
    internal static async Task<bool> IsAccountActiveAndNotLockedOutAsync<TUser>(SignInManager<TUser> signInManager, TUser user)
        where TUser : BaseUser, new()
    {
        return user.IsActive && !await signInManager.UserManager.IsLockedOutAsync(user);
    }

    /// <summary>
    /// Registers <paramref name="rawToken"/> with <paramref name="replayCache"/> keyed on a hash of
    /// the raw token string, never the token itself, retained until <paramref name="principal"/>'s
    /// own <c>exp</c> claim plus <see cref="TokenValidatorClockSkew"/> - the entire window in which
    /// the token could still validate again.
    /// </summary>
    /// <remarks>
    /// A principal carrying no readable <c>exp</c> is rejected rather than exempted. There is no
    /// window to bound the entry with, so remembering it would either leak or expire arbitrarily.
    /// <see cref="OidcExternalTokenValidator"/> requires <c>exp</c> for the principal to exist at
    /// all, so this only reaches a host that has replaced <see cref="IExternalTokenValidator"/> with
    /// one that does not surface it - and that host should surface it rather than lose the check.
    /// </remarks>
    private static Task<bool> IsFirstPresentationAsync(
        IExternalTokenReplayCache replayCache,
        string rawToken,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (TryGetExpiryClaimUtc(principal, out var expiresUtc) is false)
            return Task.FromResult(false);

        return replayCache.TryRegisterAsync(ComputeReplayKey(rawToken), expiresUtc + TokenValidatorClockSkew, cancellationToken);
    }

    private static bool TryGetExpiryClaimUtc(ClaimsPrincipal principal, out DateTimeOffset expiresUtc)
    {
        expiresUtc = default;

        var expiry = principal.FindFirst("exp")?.Value;
        if (expiry is null || !long.TryParse(expiry, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
            return false;

        expiresUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        return true;
    }

    private static string ComputeReplayKey(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}

