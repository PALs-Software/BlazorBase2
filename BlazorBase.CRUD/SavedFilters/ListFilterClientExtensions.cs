using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// DI registration of the <see cref="IListFilterStore"/> implementations. Safe for WASM and MAUI clients: nothing here
/// touches server-only types.
/// </summary>
public static class ListFilterClientExtensions
{
    /// <summary>
    /// Registers <see cref="HttpListFilterStore"/>: saved filters are stored on the server and follow the user across
    /// browsers and devices. The server must map <c>MapBlazorBaseListFilters</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureClient">Sets the base address, which must be the list-filter route (for example <c>api/list-filters/</c>) ending in a slash.</param>
    /// <param name="configureHttpClientBuilder">Adds message handlers such as the CSRF or token handler.</param>
    public static IServiceCollection AddBlazorBaseListFiltersClient(
        this IServiceCollection services,
        Action<HttpClient> configureClient,
        Action<IHttpClientBuilder>? configureHttpClientBuilder = null)
    {
        var builder = services.AddHttpClient<IListFilterStore, HttpListFilterStore>(configureClient);
        configureHttpClientBuilder?.Invoke(builder);
        return services;
    }

    /// <summary>
    /// Registers <see cref="LocalStorageListFilterStore"/>: saved filters live in the browser, per browser profile.
    /// </summary>
    public static IServiceCollection AddBlazorBaseListFiltersLocalStorage(this IServiceCollection services)
    {
        services.AddScoped<IListFilterStore, LocalStorageListFilterStore>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="InMemoryListFilterStore"/>: saved filters last until the application restarts.
    /// </summary>
    public static IServiceCollection AddBlazorBaseListFiltersInMemory(this IServiceCollection services)
    {
        services.AddSingleton<IListFilterStore, InMemoryListFilterStore>();
        return services;
    }
}
