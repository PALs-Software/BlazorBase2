using Microsoft.AspNetCore.Http;

namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// The origin check shared by <see cref="BlazorBaseCsrfMiddleware"/> and
/// <see cref="IdentityCookieAuthTransport.IsRequestIntentionalAsync"/> — the middleware is now the
/// primary defence for every state-changing request, the transport keeps <c>refresh</c>/<c>logout</c>
/// covered in depth in case a host somehow bypasses the middleware.
/// </summary>
internal static class BlazorBaseCsrfChecks
{
    /// <summary>
    /// <c>none</c> covers a request the user initiated directly (typing a URL, a bookmark), which no
    /// other site can cause. An absent header means a client that does not send fetch metadata, and
    /// is left to the antiforgery token check rather than blocked outright. <c>same-site</c> — a
    /// different subdomain of the same registrable domain — only passes when
    /// <paramref name="allowSameSiteRequests"/> opts into it; see
    /// <see cref="AuthCookieOptions.AllowSameSiteRequests"/> for why that is not the default.
    /// </summary>
    public static bool IsSameOriginRequest(HttpContext httpContext, bool allowSameSiteRequests)
    {
        var fetchSite = httpContext.Request.Headers["Sec-Fetch-Site"].ToString();

        if (string.IsNullOrEmpty(fetchSite))
            return true;

        if (fetchSite is "same-origin" or "none")
            return true;

        return allowSameSiteRequests && fetchSite == "same-site";
    }
}

