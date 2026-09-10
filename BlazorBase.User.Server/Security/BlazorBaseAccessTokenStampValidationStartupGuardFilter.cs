using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace BlazorBase.User.Server.Security;

/// <summary>
/// Fails every request rather than letting an access token silently keep authenticating past its own
/// revocation. Registered only when <c>AddBlazorBaseUserServer</c> claims the authentication default
/// (<see cref="Configuration.BlazorBaseUserServerOptions.ClaimDefaultScheme"/>) and
/// <c>AuthTokens:SecurityStampValidationInterval</c> has not opted the whole check out with a negative
/// value - a host that already dispatches its own authentication schemes
/// (<c>ClaimDefaultScheme = false</c>) has declared it manages that wiring itself, so this guard does
/// not apply to it.
/// </summary>
/// <remarks>
/// Same shape as <see cref="BlazorBase.User.Server.Tokens.BlazorBaseCsrfStartupGuardFilter"/>: the
/// guard middleware this installs sits before anything the host's own pipeline code adds, so it
/// inspects <see cref="BlazorBaseAccessTokenStampValidationMiddlewareRegistration.WasRegistered"/> only
/// after that code has already run and the flag has its final value.
/// </remarks>
internal sealed class BlazorBaseAccessTokenStampValidationStartupGuardFilter(
    BlazorBaseAccessTokenStampValidationMiddlewareRegistration registration) : IStartupFilter
{
    #region Injects
    private readonly BlazorBaseAccessTokenStampValidationMiddlewareRegistration Registration = registration;
    #endregion

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (httpContext, requestDelegate) =>
        {
            if (!Registration.WasRegistered)
                throw new InvalidOperationException(
                    "AddBlazorBaseUserServer() claims the authentication default and security-stamp revalidation is " +
                    "enabled, but app.UseBlazorBaseAccessTokenStampValidation() was never called. An access token " +
                    "issued before a password reset or a security-stamp rotation keeps authenticating until it " +
                    "expires. Call app.UseBlazorBaseAccessTokenStampValidation() after app.UseAuthentication() and " +
                    "before app.UseAuthorization(), or set AuthTokens:SecurityStampValidationInterval to a negative " +
                    "value to opt out.");

            await requestDelegate(httpContext);
        });

        next(app);
    };
}
