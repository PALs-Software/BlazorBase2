using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
/// the case <c>SameSite=Lax</c> misses: "same site" is registrable-domain scoped, so a hostile or
/// compromised sibling subdomain counts as same-site and its requests carry the session cookie.
///
/// The double-submit check compares a readable cookie against a header. A cross-site attacker can
/// make the browser send cookies but cannot set a custom header without a CORS preflight, which a
/// correctly configured server refuses. It therefore still holds where the fetch-metadata header is
/// absent, and it fails closed rather than open.
///
/// Safe methods pass through. That is only sound while <c>GET</c> has no side effects — a rule the
/// application owns, since no filter can tell a reading <c>GET</c> from a writing one.
/// </remarks>
public sealed class BlazorBaseCsrfEndpointFilter(IOptions<AuthCookieOptions> options) : IEndpointFilter
{
    #region Injects
    private readonly AuthCookieOptions Options = options.Value;
    #endregion

    private static readonly string[] SafeMethods = [HttpMethods.Get, HttpMethods.Head, HttpMethods.Options, HttpMethods.Trace];

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        if (SafeMethods.Contains(httpContext.Request.Method, StringComparer.OrdinalIgnoreCase))
            return await next(context);

        if (!IsSameOriginRequest(httpContext))
            return Results.Forbid();

        if (!HasMatchingCsrfToken(httpContext))
            return Results.Forbid();

        return await next(context);
    }

    /// <summary>
    /// <c>none</c> covers a request the user initiated directly (typing a URL, a bookmark), which no
    /// other site can cause. An absent header means a client that does not send fetch metadata, and
    /// is left to the double-submit check rather than blocked outright.
    /// </summary>
    private static bool IsSameOriginRequest(HttpContext httpContext)
    {
        var fetchSite = httpContext.Request.Headers["Sec-Fetch-Site"].ToString();

        if (string.IsNullOrEmpty(fetchSite))
            return true;

        return fetchSite is "same-origin" or "none";
    }

    private bool HasMatchingCsrfToken(HttpContext httpContext)
    {
        if (!httpContext.Request.Cookies.TryGetValue(Options.CsrfCookieName, out var cookieToken) || string.IsNullOrEmpty(cookieToken))
            return false;

        var headerToken = httpContext.Request.Headers[Options.CsrfHeaderName].ToString();

        if (string.IsNullOrEmpty(headerToken))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(cookieToken),
            Encoding.UTF8.GetBytes(headerToken));
    }
}
