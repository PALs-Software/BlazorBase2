using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.Dashboard.Server;

/// <summary>
/// Server-side DI registration for dashboard layouts. Server-only: do not call it from a WASM client.
/// </summary>
public static class DashboardLayoutServerExtensions
{
    /// <summary>
    /// Registers <see cref="EfDashboardLayoutRepository{TContext}"/> over the host's <typeparamref name="TContext"/>, which
    /// must be registered as a context factory and include <see cref="SavedDashboardLayout"/> in its model.
    /// </summary>
    public static IServiceCollection AddBlazorBaseDashboardLayouts<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddScoped<IDashboardLayoutRepository, EfDashboardLayoutRepository<TContext>>();
        return services;
    }
}
