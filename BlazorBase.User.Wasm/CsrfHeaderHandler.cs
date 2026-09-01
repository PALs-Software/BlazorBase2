using BlazorBase.User.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace BlazorBase.User.Wasm;

/// <summary>
/// Attaches the antiforgery token to every state-changing request and makes the browser send the
/// session cookies. Required by the cookie transport; harmless without it.
/// </summary>
/// <remarks>
/// Registered once on the API <c>HttpClient</c> rather than left to call sites. One forgotten call is
/// one endpoint that rejects every write, and that gets found in production rather than in review.
///
/// The token comes from <c>GET /api/auth/session</c> and is held in memory. It is deliberately not
/// read from a cookie: ASP.NET Core binds the token to the caller's identity, and delivering it in
/// the body keeps it out of reach of anything that can merely write cookies.
/// </remarks>
public sealed class CsrfHeaderHandler(CsrfTokenProvider tokenProvider, ICsrfTokenOptions options) : DelegatingHandler
{
    #region Injects
    private readonly CsrfTokenProvider TokenProvider = tokenProvider;
    private readonly ICsrfTokenOptions Options = options;
    #endregion

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        // A missing token is not an error here. The request goes out without the header and the
        // server refuses it, which is the same answer decided in the one place able to decide it.
        if (RequiresCsrfToken(request.Method) && TokenProvider.Token is { } token)
        {
            request.Headers.Remove(Options.HeaderName);
            request.Headers.Add(Options.HeaderName, token);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private static bool RequiresCsrfToken(HttpMethod method) =>
        method != HttpMethod.Get && method != HttpMethod.Head && method != HttpMethod.Options && method != HttpMethod.Trace;
}
