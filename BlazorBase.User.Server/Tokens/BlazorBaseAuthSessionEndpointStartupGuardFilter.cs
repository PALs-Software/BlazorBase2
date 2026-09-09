using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Fails every request rather than letting a host silently leave its client with no way to read who
/// it is signed in as. Identity's bearer access token is an opaque data-protected blob and the cookie
/// transport's is <c>HttpOnly</c>, so under either transport the client can only learn its claims -
/// and, under the cookie transport, obtain an antiforgery token - from
/// <c>app.MapBlazorBaseAuthSessionEndpoint()</c>. Forgetting the call leaves authentication silently
/// dead: every caller reads as signed out and nothing says why.
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
                    "app.MapBlazorBaseAuthSessionEndpoint() was never called, but this host requires it. " +
                    "A BlazorBase client cannot decode its own claims from either token transport and reads every " +
                    "caller as signed out without it, so authentication is silently dead. Call " +
                    "app.MapBlazorBaseAuthSessionEndpoint() before app.MapFallbackToFile(...). If no BlazorBase " +
                    "client consumes this API, set BlazorBaseUserServerOptions.RequiresAuthSessionEndpoint to false " +
                    "in AddBlazorBaseUserServer() - note that AddBlazorBaseCookieAuthTokens() requires the endpoint " +
                    "regardless of that flag.");

            await requestDelegate(httpContext);
        });

        next(app);
    };
}
