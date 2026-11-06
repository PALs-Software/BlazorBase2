using BlazorBase.User.Services;
using Microsoft.Extensions.DependencyInjection;
using BlazorBase.Components.Services;

namespace BlazorBase.User.Wasm;

public static class BlazorBaseUserWasmServiceCollectionExtensions
{
    /// <summary>
    /// Registers the WASM-specific user/auth platform services:
    /// <see cref="ITokenStorage"/>, <see cref="WasmAppConfigService"/> as <see cref="IAppConfigService"/>,
    /// <see cref="WasmFormFactor"/> as <see cref="IFormFactor"/>, and <see cref="SessionExpiryHandler"/>.
    /// The base address is fixed to the host's base address (same-origin API).
    /// </summary>
    /// <remarks>
    /// The token storage and the client session state
    /// (<see cref="BlazorBaseUserServiceCollectionExtensions.AddBlazorBaseUserSharedClientSession"/>) are
    /// one instance for the whole app. IHttpClientFactory resolves <see cref="AuthTokenHandler"/> in a
    /// scope of its own, so a scoped <see cref="InMemoryTokenStorage"/> held the tokens the login page saved
    /// while the handler read an empty copy, and every API call was answered 401.
    /// </remarks>
    /// <param name="persistTokensInBrowserStorage">
    /// Writes both tokens to <c>localStorage</c> (<see cref="BrowserTokenStorage"/>) instead of the
    /// default <see cref="InMemoryTokenStorage"/>. In-memory storage means a browser reload signs a
    /// bearer-mode user out — the refresh token that would recover the session is lost with it. That
    /// is the correct trade-off for a browser, and it is the reason the cookie transport
    /// (<see cref="AddBlazorBaseUserWasmCookieAuth"/>) is the recommended mode for one. Set this only
    /// when the host cannot use the cookie transport and accepts a token sitting in script-readable
    /// storage in exchange for surviving a reload.
    /// </param>
    public static IServiceCollection AddBlazorBaseUserWasm(
        this IServiceCollection services,
        string hostBaseAddress,
        bool persistTokensInBrowserStorage = false)
    {
        if (persistTokensInBrowserStorage)
            services.AddSingleton<ITokenStorage, BrowserTokenStorage>();
        else
            services.AddSingleton<ITokenStorage, InMemoryTokenStorage>();

        services.AddBlazorBaseUserSharedClientSession();

        services.AddScoped<IAppConfigService>(_ => new WasmAppConfigService(hostBaseAddress));
        services.AddScoped<IFormFactor, WasmFormFactor>();
        services.AddScoped<SessionExpiryHandler>();
        return services;
    }

    /// <summary>
    /// The cookie (backend-for-frontend) counterpart of <see cref="AddBlazorBaseUserWasm"/>: no token
    /// storage, authentication state read from the server, and the CSRF header attached to every
    /// write. Use instead of <see cref="AddBlazorBaseUserWasm"/> when the server was configured with
    /// <c>AddBlazorBaseCookieAuthTokens</c>. The recommended mode for a browser host, because the
    /// session survives a reload without holding a token the client can read.
    /// </summary>
    /// <remarks>
    /// Register the handlers on every API <c>HttpClient</c> yourself — the framework cannot know which
    /// of a host's typed clients talk to the API. The auth client gets <see cref="CsrfHeaderHandler"/>
    /// only, every other client
    /// <see cref="CookieSessionHttpClientBuilderExtensions.AddBlazorBaseCookieSessionHandlers"/>:
    /// <code>
    /// services.AddHttpClient&lt;IAuthService, AuthService&gt;(c => c.BaseAddress = new Uri(baseAddress))
    ///         .AddHttpMessageHandler&lt;CsrfHeaderHandler&gt;();
    /// services.AddHttpClient&lt;IUserService, UserService&gt;(c => c.BaseAddress = new Uri(baseAddress))
    ///         .AddBlazorBaseCookieSessionHandlers();
    /// </code>
    /// The antiforgery token and the session state are one instance for the whole app
    /// (<see cref="BlazorBaseUserServiceCollectionExtensions.AddBlazorBaseUserSharedClientSession"/>),
    /// because IHttpClientFactory resolves <see cref="CsrfHeaderHandler"/> in a scope of its own and a
    /// scoped token never reached it.
    /// Do <b>not</b> also register <c>AuthTokenHandler</c>. It exists to attach and refresh a token
    /// the client holds, and under this transport there is none — it would attach nothing and refresh
    /// against a body the server ignores.
    /// </remarks>
    public static IServiceCollection AddBlazorBaseUserWasmCookieAuth(
        this IServiceCollection services,
        string hostBaseAddress,
        Action<CsrfTokenOptions>? configureCsrf = null)
    {
        var csrfOptions = new CsrfTokenOptions();
        configureCsrf?.Invoke(csrfOptions);
        csrfOptions.ApiOrigin = new Uri(hostBaseAddress, UriKind.Absolute);

        services.AddScoped<ITokenStorage, NoOpTokenStorage>();
        services.AddScoped<IAppConfigService>(_ => new WasmAppConfigService(hostBaseAddress));
        services.AddScoped<IFormFactor, WasmFormFactor>();
        services.AddBlazorBaseUserSharedClientSession();

        services.AddSingleton<ICsrfTokenOptions>(csrfOptions);
        services.AddScoped<CsrfHeaderHandler>();
        services.AddScoped<SessionExpiryHandler>();

        return services;
    }
}

