using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Fails every request rather than letting the cookie transport's CSRF protection silently do
/// nothing. A host that registers <c>AddBlazorBaseCookieAuthTokens</c> is opting into a cookie that
/// authenticates cross-site requests automatically; if it then forgets <c>app.UseBlazorBaseCsrf()</c>,
/// every state-changing endpoint is forgeable and nothing about the running app says so.
/// </summary>
/// <remarks>
/// Registered as an <see cref="IStartupFilter"/> so the check runs regardless of whether the host
/// wires its pipeline through a <c>Startup</c> class or directly on a minimal-hosting
/// <c>WebApplication</c> — both call every registered filter while building the request pipeline.
/// The guard middleware this installs sits before anything the host's own <c>Configure</c>/pipeline
/// code adds, so it inspects <see cref="BlazorBaseCsrfMiddlewareRegistration.WasRegistered"/> only
/// after that code has already run and the flag has its final value; on a correctly configured host
/// the flag is <see langword="true"/> and the guard is a plain pass-through, so it cannot itself
/// break a working pipeline.
/// </remarks>
internal sealed class BlazorBaseCsrfStartupGuardFilter(BlazorBaseCsrfMiddlewareRegistration registration) : IStartupFilter
{
    #region Injects
    private readonly BlazorBaseCsrfMiddlewareRegistration Registration = registration;
    #endregion

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (httpContext, requestDelegate) =>
        {
            if (!Registration.WasRegistered)
                throw new InvalidOperationException(
                    "AddBlazorBaseCookieAuthTokens() is registered, but app.UseBlazorBaseCsrf() was never called. " +
                    "Every state-changing request is currently forgeable from another site. Call app.UseBlazorBaseCsrf() " +
                    "after app.UseAuthentication() to enable the check, or stop registering the cookie transport.");

            await requestDelegate(httpContext);
        });

        next(app);
    };
}
