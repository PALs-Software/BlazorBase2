using BlazorBase.User.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.Wasm;

public static class CookieSessionHttpClientBuilderExtensions
{
    /// <summary>
    /// Adds the two handlers every API client needs under the cookie transport:
    /// <see cref="CsrfHeaderHandler"/> (session cookies and the antiforgery header) and
    /// <see cref="SessionExpiryHandler"/> (drops the UI to the login screen on a 401).
    /// </summary>
    /// <remarks>
    /// Not for the <see cref="IAuthService"/> client. <see cref="SessionExpiryHandler"/> depends on
    /// <see cref="BlazorBaseUserAuthStateProvider"/>, which depends on <see cref="IAuthService"/>, so the
    /// auth client registers <see cref="CsrfHeaderHandler"/> alone.
    /// </remarks>
    public static IHttpClientBuilder AddBlazorBaseCookieSessionHandlers(this IHttpClientBuilder httpClientBuilder) =>
        httpClientBuilder
            .AddHttpMessageHandler<CsrfHeaderHandler>()
            .AddHttpMessageHandler<SessionExpiryHandler>();
}
