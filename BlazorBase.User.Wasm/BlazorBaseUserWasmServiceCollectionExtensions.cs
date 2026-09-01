using BlazorBase.User.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using BlazorBase.Components.Services;

namespace BlazorBase.User.Wasm;

public static class BlazorBaseUserWasmServiceCollectionExtensions
{
    /// <summary>
    /// Registers the WASM-specific user/auth platform services:
    /// <see cref="BrowserTokenStorage"/> as <see cref="ITokenStorage"/>,
    /// <see cref="WasmAppConfigService"/> as <see cref="IAppConfigService"/> and
    /// <see cref="WasmFormFactor"/> as <see cref="IFormFactor"/>.
    /// The base address is fixed to the host's base address (same-origin API).
    /// </summary>
    public static IServiceCollection AddBlazorBaseUserWasm(this IServiceCollection services, string hostBaseAddress)
    {
        services.AddScoped<ITokenStorage, BrowserTokenStorage>();
        services.AddScoped<IAppConfigService>(_ => new WasmAppConfigService(hostBaseAddress));
        services.AddScoped<IFormFactor, WasmFormFactor>();
        return services;
    }

    /// <summary>
    /// The cookie (backend-for-frontend) counterpart of <see cref="AddBlazorBaseUserWasm"/>: no token
    /// storage, authentication state read from the server, and the CSRF header attached to every
    /// write. Use instead of <see cref="AddBlazorBaseUserWasm"/> when the server was configured with
    /// <c>AddBlazorBaseCookieAuthTokens</c>.
    /// </summary>
    /// <remarks>
    /// Register <see cref="CsrfHeaderHandler"/> on the API <c>HttpClient</c> yourself — the framework
    /// cannot know which of a host's typed clients talk to the API:
    /// <code>
    /// services.AddHttpClient&lt;IAuthService, AuthService&gt;(c => c.BaseAddress = new Uri(baseAddress))
    ///         .AddHttpMessageHandler&lt;CsrfHeaderHandler&gt;();
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
        services.AddScoped<CsrfTokenProvider>();
        services.AddScoped<CsrfHeaderHandler>();

        // Replaces the token-parsing provider registered by AddBlazorBaseUserClient, whichever order
        // the two calls happen in.
        services.RemoveAll<BlazorBaseUserAuthStateProvider>();
        services.RemoveAll<AuthenticationStateProvider>();
        services.AddScoped<CookieAuthStateProvider>();
        services.AddScoped<BlazorBaseUserAuthStateProvider>(sp => sp.GetRequiredService<CookieAuthStateProvider>());
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CookieAuthStateProvider>());

        return services;
    }
}
