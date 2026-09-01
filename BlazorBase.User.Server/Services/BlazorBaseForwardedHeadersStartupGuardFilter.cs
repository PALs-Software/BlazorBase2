using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorBase.User.Server.Services;

/// <summary>
/// Logs a startup warning, rather than failing the build, when <c>AddBlazorBaseUserServer</c>'s
/// unconditional "BlazorBaseAuth" rate limiter has no trusted proxy or network configured through
/// <c>AddBlazorBaseForwardedHeaders</c>.
/// </summary>
/// <remarks>
/// A warning, not an exception, because a host with no reverse proxy in front of it (a container
/// reached directly, most local development) is correctly configured by doing nothing - the
/// connection's <c>RemoteIpAddress</c> already is the client's. Failing startup here would punish
/// that host for a proxy it does not have. A host that does sit behind one only learns about the
/// single shared rate-limit bucket from this log line, so it is written once, at startup, instead of
/// once per request.
/// </remarks>
internal sealed class BlazorBaseForwardedHeadersStartupGuardFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        var marker = app.ApplicationServices.GetService<ForwardedHeadersConfigurationMarker>();

        if (marker is null || !marker.HasKnownProxiesOrNetworks)
        {
            app.ApplicationServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("BlazorBase.User.Server.ForwardedHeaders")
                .LogWarning(
                    "The 'BlazorBaseAuth' rate limiter and the brute-force delay both partition by " +
                    "HttpContext.Connection.RemoteIpAddress. No known proxy or network was configured " +
                    "through AddBlazorBaseForwardedHeaders(configuration). Behind a reverse proxy this " +
                    "address is the proxy's own, so every client currently shares one rate-limit bucket. " +
                    "If this app is not behind a reverse proxy, no action is needed.");
        }

        next(app);
    };
}
