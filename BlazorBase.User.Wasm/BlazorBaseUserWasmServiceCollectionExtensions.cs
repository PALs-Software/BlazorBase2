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
            services.AddScoped<ITokenStorage, BrowserTokenStorage>();
        else
            services.AddScoped<ITokenStorage, InMemoryTokenStorage>();

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
    /// Register <see cref="CsrfHeaderHandler"/> and <see cref="SessionExpiryHandler"/> on the API
    /// <c>HttpClient</c> yourself — the framework cannot know which of a host's typed clients talk to
    /// the API:
    /// <code>
    /// services.AddHttpClient&lt;IAuthService, AuthService&gt;(c => c.BaseAddress = new Uri(baseAddress))
    ///         .AddHttpMessageHandler&lt;CsrfHeaderHandler&gt;()
    ///         .AddHttpMessageHandler&lt;SessionExpiryHandler&gt;();
    /// </code>
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

        services.AddScoped<ITokenStorage, NoOpTokenStorage>();
        services.AddScoped<IAppConfigService>(_ => new WasmAppConfigService(hostBaseAddress));
        services.AddScoped<IFormFactor, WasmFormFactor>();

        services.AddSingleton<ICsrfTokenOptions>(csrfOptions);
        services.AddScoped<CsrfHeaderHandler>();
        services.AddScoped<SessionExpiryHandler>();

        return services;
    }
}

