using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.Server.Security;

/// <summary>
/// Opt-in registration for <see cref="BlazorBaseAccessTokenStampValidationMiddleware"/>.
/// </summary>
public static class BlazorBaseAccessTokenStampValidationExtensions
{
    /// <summary>
    /// Enforces <see cref="BlazorBaseAccessTokenStampValidationMiddleware"/> for every bearer-authenticated
    /// request from this point in the pipeline onward. Place it after <c>app.UseAuthentication()</c> and
    /// before <c>app.UseAuthorization()</c> - the caller's identity must already be resolved, and
    /// authorization must not run against a stamp that turned out to be stale.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <c>AddBlazorBaseUserServer</c> was never called - this middleware has nothing to validate against.
    /// </exception>
    public static IApplicationBuilder UseBlazorBaseAccessTokenStampValidation(this IApplicationBuilder app)
    {
        _ = app.ApplicationServices.GetService<AccessTokenStampValidationOptions>()
            ?? throw new InvalidOperationException(
                "app.UseBlazorBaseAccessTokenStampValidation() requires AddBlazorBaseUserServer() to have been called first.");

        var registration = app.ApplicationServices.GetService<BlazorBaseAccessTokenStampValidationMiddlewareRegistration>();

        if (registration is not null)
            registration.WasRegistered = true;

        return app.UseMiddleware<BlazorBaseAccessTokenStampValidationMiddleware>();
    }
}
