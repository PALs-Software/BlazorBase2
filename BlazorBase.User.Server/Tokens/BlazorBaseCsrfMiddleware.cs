using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Rejects state-changing requests that a cross-site page could have triggered. Runs as middleware,
/// not an endpoint filter, because an endpoint filter cannot see an MVC controller action at all —
/// which is exactly why <c>UserControllerBase.UpdateSettings</c> stayed unprotected under the old
/// per-endpoint <c>RequireBlazorBaseCsrf()</c> opt-in, and why every other host controller action was
/// unprotected along with it.
/// </summary>
/// <remarks>
/// Two independent checks, because each has a gap the other covers.
///
/// <c>Sec-Fetch-Site</c> is set by the browser and cannot be forged by page script. It also catches
/// the case <c>SameSite</c> misses: "same site" is registrable-domain scoped, so a hostile or
/// compromised sibling subdomain counts as same-site and its requests carry the session cookie. It is
/// absent on older clients, which is why it cannot be the only check. A same-site request only passes
/// this half of the check when <see cref="AuthCookieOptions.AllowSameSiteRequests"/> opts into it -
/// see that option for why the default keeps rejecting it.
///
/// <see cref="IAntiforgery"/> does the token half. Its request token is data-protected and bound to
/// the caller's identity, so a token minted for someone else does not validate. A bare random value
/// in a readable cookie does not achieve that: a subdomain able to write cookies can plant both
/// halves of a double-submit pair and satisfy the check.
///
/// A request carrying an <c>Authorization</c> header bypasses both checks: attaching that header is
/// something only a deliberate caller does, which is the proof of intent a cookie cannot give. This is
/// decided on the header's mere presence, not on what it authenticates to, because the middleware runs
/// ahead of authorization in the pipeline and has no result to inspect yet.
///
/// Safe methods pass through untouched. That is only sound while <c>GET</c> has no side effects — a
/// rule the application owns, since no middleware can tell a reading <c>GET</c> from a writing one.
/// </remarks>
public sealed class BlazorBaseCsrfMiddleware(RequestDelegate next, IAntiforgery antiforgery, IOptionsMonitor<AuthCookieOptions> cookieOptions)
{
    #region Injects
    private readonly RequestDelegate Next = next;
    private readonly IAntiforgery Antiforgery = antiforgery;
    private readonly IOptionsMonitor<AuthCookieOptions> CookieOptions = cookieOptions;
    #endregion

    private static readonly string[] SafeMethods = [HttpMethods.Get, HttpMethods.Head, HttpMethods.Options, HttpMethods.Trace];

    public async Task InvokeAsync(HttpContext httpContext)
    {
        if (SafeMethods.Contains(httpContext.Request.Method, StringComparer.OrdinalIgnoreCase))
        {
            await Next(httpContext);
            return;
        }

        if (!string.IsNullOrEmpty(httpContext.Request.Headers.Authorization.ToString()))
        {
            await Next(httpContext);
            return;
        }

        if (httpContext.GetEndpoint()?.Metadata.GetMetadata<AllowBlazorBaseCsrfAttribute>() is not null)
        {
            await Next(httpContext);
            return;
        }

        if (!BlazorBaseCsrfChecks.IsSameOriginRequest(httpContext, CookieOptions.CurrentValue.AllowSameSiteRequests))
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (!await Antiforgery.IsRequestValidAsync(httpContext))
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await Next(httpContext);
    }
}
