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
    /// is left to the antiforgery token check rather than blocked outright.
    /// </summary>
    public static bool IsSameOriginRequest(HttpContext httpContext)
    {
        var fetchSite = httpContext.Request.Headers["Sec-Fetch-Site"].ToString();

        if (string.IsNullOrEmpty(fetchSite))
            return true;

        return fetchSite is "same-origin" or "none";
    }
}
