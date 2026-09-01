using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.JSInterop;

namespace BlazorBase.User.Wasm;

/// <summary>
/// Attaches the CSRF token to every state-changing request and makes the browser send the session
/// cookies. Required by the cookie transport; harmless without it.
/// </summary>
/// <remarks>
/// Registered once on the API <c>HttpClient</c> rather than left to call sites. One forgotten call is
/// one endpoint that rejects every write, and that gets found in production rather than in review.
///
/// The token is read from a readable cookie the server sets alongside the session. That is the
/// double-submit half of the defence: a cross-site page can make the browser attach cookies, but it
/// cannot set a custom header without a CORS preflight the server refuses.
/// </remarks>
public sealed class CsrfHeaderHandler(IJSRuntime jsRuntime, ICsrfTokenOptions options) : DelegatingHandler
{
    #region Injects
    private readonly IJSRuntime JsRuntime = jsRuntime;
    private readonly ICsrfTokenOptions Options = options;
    #endregion

    private IJSObjectReference? _module;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        if (RequiresCsrfToken(request.Method))
        {
            var token = await ReadTokenAsync(cancellationToken);

            if (!string.IsNullOrEmpty(token))
                request.Headers.Remove(Options.HeaderName);

            if (!string.IsNullOrEmpty(token))
                request.Headers.Add(Options.HeaderName, token);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private static bool RequiresCsrfToken(HttpMethod method) =>
        method != HttpMethod.Get && method != HttpMethod.Head && method != HttpMethod.Options && method != HttpMethod.Trace;

    /// <summary>
    /// A missing token is not an error here. The request goes out without the header and the server
    /// refuses it, which is the same answer with the decision made in the one place that can make it.
    /// </summary>
    private async Task<string?> ReadTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            _module ??= await JsRuntime.InvokeAsync<IJSObjectReference>(
                "import", cancellationToken, "./_content/BlazorBase.User.Wasm/js/blazorbase-cookies.js");

            return await _module.InvokeAsync<string?>("readCookie", cancellationToken, Options.CookieName);
        }
        catch (JSException)
        {
            return null;
        }
    }
}
