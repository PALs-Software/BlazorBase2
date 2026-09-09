using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Fails every request rather than letting the cookie transport silently leave the client with no way
/// to authenticate. A host that registers <c>AddBlazorBaseCookieAuthTokens</c> hands the access token
/// to the browser as an <c>HttpOnly</c> cookie, so the client cannot decode its own claims and has no
/// antiforgery token to attach to state-changing requests unless the host also maps
/// <c>app.MapBlazorBaseAuthSessionEndpoint()</c>; forgetting it leaves authentication silently dead.
/// </summary>
/// <remarks>
/// Registered as an <see cref="IStartupFilter"/> so the check runs regardless of whether the host
/// wires its pipeline through a <c>Startup</c> class or directly on a minimal-hosting
/// <c>WebApplication</c> — both call every registered filter while building the request pipeline.
/// The guard middleware this installs sits before anything the host's own <c>Configure</c>/pipeline
/// code adds, so it inspects
/// <see cref="BlazorBaseAuthSessionEndpointRegistration.WasRegistered"/> only after that code has
/// already run and the flag has its final value; on a correctly configured host the flag is
/// <see langword="true"/> and the guard is a plain pass-through, so it cannot itself break a working
/// pipeline.
/// </remarks>
internal sealed class BlazorBaseAuthSessionEndpointStartupGuardFilter(BlazorBaseAuthSessionEndpointRegistration registration) : IStartupFilter
{
    #region Injects
    private readonly BlazorBaseAuthSessionEndpointRegistration Registration = registration;
    #endregion

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (httpContext, requestDelegate) =>
        {
            if (!Registration.WasRegistered)
                throw new InvalidOperationException(
                    "AddBlazorBaseCookieAuthTokens() is registered, but app.MapBlazorBaseAuthSessionEndpoint() was never called. " +
                    "The client has no way to read its own claims from an HttpOnly cookie and gets no antiforgery token, so " +
                    "authentication is silently dead. Call app.MapBlazorBaseAuthSessionEndpoint() before app.MapFallbackToFile(...), " +
                    "or stop registering the cookie transport.");

            await requestDelegate(httpContext);
        });

        next(app);
    };
}
