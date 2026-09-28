using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace BlazorBase.User.Server.Services;

/// <summary>
/// Marks every <c>/api/auth/*</c> response <c>Cache-Control: no-store</c>.
/// </summary>
/// <remarks>
/// Registered unconditionally by <c>AddBlazorBaseUserServer</c>, not opt-in: a bearer token, a
/// refresh token, or the session endpoint's claim list must never be served from a shared HTTP cache
/// or replayed from a browser's back-button history, and there is no configuration under which that
/// would be desirable.
/// </remarks>
internal sealed class BlazorBaseAuthNoStoreStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (httpContext, requestDelegate) =>
        {
            if (httpContext.Request.Path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase))
            {
                httpContext.Response.OnStarting(() =>
                {
                    httpContext.Response.Headers.CacheControl = "no-store";
                    return Task.CompletedTask;
                });
            }

            await requestDelegate(httpContext);
        });

        next(app);
    };
}
