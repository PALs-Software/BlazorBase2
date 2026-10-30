using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.CRUD.SavedFilters.Server;

/// <summary>
/// Server-side DI registration for saved list filters. Server-only: do not call it from a WASM client.
/// </summary>
public static class ListFilterServerExtensions
{
    /// <summary>
    /// Registers <see cref="EfListFilterRepository{TContext}"/> over the host's <typeparamref name="TContext"/>, which
    /// must be registered as a context factory and include <see cref="SavedListFilter"/> in its model.
    /// </summary>
    public static IServiceCollection AddBlazorBaseListFilters<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddScoped<IListFilterRepository, EfListFilterRepository<TContext>>();
        return services;
    }
}
