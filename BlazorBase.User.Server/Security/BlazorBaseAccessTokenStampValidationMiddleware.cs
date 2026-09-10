using System.Security.Claims;
using BlazorBase.User.Server.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace BlazorBase.User.Server.Security;

/// <summary>
/// Closes the window where a bearer access token outlives its own revocation: a password reset or a
/// bare security-stamp rotation invalidates a refresh token immediately, but - without this middleware
/// - the access token issued before it keeps authenticating until its own expiry, since
/// <see cref="Microsoft.AspNetCore.Authentication.BearerToken.BearerTokenOptions"/> exposes no
/// validation hook of its own. Runs only for a principal authenticated by the framework's bearer
/// scheme (<see cref="BlazorBaseUserServerOptions.SchemeName"/>); a cookie-authenticated principal is
/// already covered by Identity's own <c>SecurityStampValidator</c>, and a machine access token
/// (<c>AccessTokenAuthenticationDefaults.AuthenticationScheme</c>) is already re-checked against the
/// database, including its owner's <c>IsActive</c> flag, by <c>AccessTokenService</c> on every request.
/// </summary>
/// <remarks>
/// Registered by <c>app.UseBlazorBaseAccessTokenStampValidation()</c>, which must run after
/// <c>app.UseAuthentication()</c> and before <c>app.UseAuthorization()</c> - the antiforgery-style
/// position <c>app.UseBlazorBaseCsrf()</c> also requires, and for the same reason: the caller's
/// identity has to be resolved first. See
/// <see cref="BlazorBaseAccessTokenStampValidationStartupGuardFilter"/> for what happens when a host
/// that needs the call forgets it.
/// </remarks>
internal sealed class BlazorBaseAccessTokenStampValidationMiddleware(
    RequestDelegate next,
    AccessTokenStampValidationOptions options,
    AccessTokenStampValidationCache cache,
    BlazorBaseUserServerOptions serverOptions,
    TimeProvider timeProvider)
{
    #region Injects
    private readonly RequestDelegate Next = next;
    private readonly AccessTokenStampValidationOptions Options = options;
    private readonly AccessTokenStampValidationCache Cache = cache;
    private readonly BlazorBaseUserServerOptions ServerOptions = serverOptions;
    private readonly TimeProvider TimeProvider = timeProvider;
    #endregion

    public async Task InvokeAsync(HttpContext httpContext, IAccessTokenStampValidator validator)
    {
        if (!Options.Enabled || string.IsNullOrEmpty(httpContext.Request.Headers.Authorization))
        {
            await Next(httpContext);
            return;
        }

        var bearerResult = await AuthenticateAgainstTheBearerSchemeByNameAsync(httpContext);

        if (!bearerResult.Succeeded || bearerResult.Principal is null)
        {
            await Next(httpContext);
            return;
        }

        var userId = bearerResult.Principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId is null || Cache.WasRecentlyValidated(userId, Options.Interval, TimeProvider))
        {
            await Next(httpContext);
            return;
        }

        if (await validator.IsCurrentAsync(bearerResult.Principal))
            Cache.MarkValidated(userId, TimeProvider);
        else
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        await Next(httpContext);
    }

    /// <summary>
    /// Re-authenticates by scheme name rather than trusting <c>httpContext.User</c> as-is: the
    /// principal <see cref="Microsoft.AspNetCore.Identity.SignInManager{TUser}.CreateUserPrincipalAsync"/>
    /// builds - and therefore every bearer access token carries - has its
    /// <see cref="System.Security.Principal.IIdentity.AuthenticationType"/> hardcoded to
    /// <c>IdentityConstants.ApplicationScheme</c> by Identity's own claims factory (the same principal
    /// shape a cookie sign-in uses), so it never actually equals the bearer scheme's own name and
    /// cannot be used to tell the two transports apart.
    /// </summary>
    private Task<AuthenticateResult> AuthenticateAgainstTheBearerSchemeByNameAsync(HttpContext httpContext) =>
        httpContext.AuthenticateAsync(ServerOptions.SchemeName);
}
