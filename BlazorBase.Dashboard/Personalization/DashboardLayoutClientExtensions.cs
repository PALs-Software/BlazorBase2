using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.Dashboard;

/// <summary>
/// DI registration of the <see cref="IDashboardLayoutStore"/> implementations. Safe for WASM and MAUI clients: nothing here
/// touches server-only types. Registering one store switches the personalization of every <see cref="RoleCenter"/> on.
/// </summary>
public static class DashboardLayoutClientExtensions
{
    /// <summary>
    /// Registers <see cref="HttpDashboardLayoutStore"/>: layouts are stored on the server and follow the user across browsers
    /// and devices. The server must map <c>MapBlazorBaseDashboardLayouts</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureClient">Sets the base address, which must be the dashboard-layout route (for example <c>api/dashboard-layouts/</c>) ending in a slash.</param>
    /// <param name="configureHttpClientBuilder">Adds message handlers such as the CSRF or token handler.</param>
    public static IServiceCollection AddBlazorBaseDashboardLayoutsClient(
        this IServiceCollection services,
        Action<HttpClient> configureClient,
        Action<IHttpClientBuilder>? configureHttpClientBuilder = null)
    {
        var builder = services.AddHttpClient<IDashboardLayoutStore, HttpDashboardLayoutStore>(configureClient);
        configureHttpClientBuilder?.Invoke(builder);
        return services;
    }

    /// <summary>
    /// Registers <see cref="LocalStorageDashboardLayoutStore"/>: layouts live in the browser, per browser profile.
    /// </summary>
    public static IServiceCollection AddBlazorBaseDashboardLayoutsLocalStorage(this IServiceCollection services)
    {
        services.AddScoped<IDashboardLayoutStore, LocalStorageDashboardLayoutStore>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="InMemoryDashboardLayoutStore"/>: layouts last until the application restarts.
    /// </summary>
    public static IServiceCollection AddBlazorBaseDashboardLayoutsInMemory(this IServiceCollection services)
    {
        services.AddSingleton<IDashboardLayoutStore, InMemoryDashboardLayoutStore>();
        return services;
    }
}
