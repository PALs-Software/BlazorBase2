using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Rejects state-changing requests that a cross-site page could have triggered. Only relevant once
/// the session travels in a cookie: a cookie is attached by the browser automatically, so a request
/// forged on another site would otherwise arrive fully authenticated.
/// </summary>
/// <remarks>
/// Two independent checks, because each has a gap the other covers.
///
/// <c>Sec-Fetch-Site</c> is set by the browser and cannot be forged by page script. It also catches
/// the case <c>SameSite</c> misses: "same site" is registrable-domain scoped, so a hostile or
/// compromised sibling subdomain counts as same-site and its requests carry the session cookie. It is
/// absent on older clients, which is why it cannot be the only check.
///
/// <see cref="IAntiforgery"/> does the token half. Its request token is data-protected and bound to
/// the caller's identity, so a token minted for someone else does not validate. A bare random value
/// in a readable cookie does not achieve that: a subdomain able to write cookies can plant both
/// halves of a double-submit pair and satisfy the check.
///
/// Safe methods pass through. That is only sound while <c>GET</c> has no side effects — a rule the
/// application owns, since no filter can tell a reading <c>GET</c> from a writing one.
/// </remarks>
public sealed class BlazorBaseCsrfEndpointFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    #region Injects
    private readonly IAntiforgery Antiforgery = antiforgery;
    #endregion

    private static readonly string[] SafeMethods = [HttpMethods.Get, HttpMethods.Head, HttpMethods.Options, HttpMethods.Trace];

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        if (SafeMethods.Contains(httpContext.Request.Method, StringComparer.OrdinalIgnoreCase))
            return await next(context);

        if (!IsSameOriginRequest(httpContext))
            return Results.Forbid();

        if (!await Antiforgery.IsRequestValidAsync(httpContext))
            return Results.Forbid();

        return await next(context);
    }

    /// <summary>
    /// <c>none</c> covers a request the user initiated directly (typing a URL, a bookmark), which no
    /// other site can cause. An absent header means a client that does not send fetch metadata, and
    /// is left to the token check rather than blocked outright.
    /// </summary>
    internal static bool IsSameOriginRequest(HttpContext httpContext)
    {
        var fetchSite = httpContext.Request.Headers["Sec-Fetch-Site"].ToString();

        if (string.IsNullOrEmpty(fetchSite))
            return true;

        return fetchSite is "same-origin" or "none";
    }
}
