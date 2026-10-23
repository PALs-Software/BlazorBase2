using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlazorBase.Dashboard;

/// <summary>
/// Registration of the dashboard services.
/// </summary>
public static class DashboardServiceCollectionExtensions
{
    /// <summary>
    /// Registers the part registry, the headline insight providers and the services the role center and its building
    /// blocks resolve. Safe to call from several modules: parts and providers add up, an id may be registered once.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddBlazorBaseDashboard(options =&gt;
    /// {
    ///     options.AutoRefreshInterval = TimeSpan.FromMinutes(2);
    ///     options.AddPart&lt;MyUsagePart&gt;("my-usage", priority: 10, columns: 7);
    ///     options.AddPart&lt;BoostsPart&gt;("boosts", 20, 5, DashboardVisibility.ForRoles("Admin", "Approver"));
    ///     options.AddHeadlineInsightProvider&lt;BoostInsightProvider&gt;();
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddBlazorBaseDashboard(this IServiceCollection services, Action<DashboardOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new DashboardOptions();
        configure?.Invoke(options);

        var registry = GetOrAddInstance<DashboardPartRegistry>(services);
        var settings = GetOrAddInstance<DashboardSettings>(services);

        services.TryAddSingleton<IDashboardPartRegistry>(registry);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<DashboardInterop>();
        services.TryAddScoped<DashboardClock>();

        if (options.AutoRefreshIntervalValue is { } interval)
            settings.AutoRefreshInterval = interval;

        foreach (var part in options.Parts)
            registry.Register(part);

        foreach (var providerType in options.InsightProviderTypes)
            services.AddScoped(typeof(IHeadlineInsightProvider), providerType);

        return services;
    }

    /// <summary>
    /// Registers a scoped <see cref="IHeadlineInsightProvider"/> outside of <see cref="AddBlazorBaseDashboard"/>.
    /// </summary>
    public static IServiceCollection AddHeadlineInsightProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IHeadlineInsightProvider =>
        services.AddScoped<IHeadlineInsightProvider, TProvider>();

    private static T GetOrAddInstance<T>(IServiceCollection services)
        where T : class, new()
    {
        var existing = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(T) && descriptor.ImplementationInstance is T);

        if (existing?.ImplementationInstance is T instance)
            return instance;

        var created = new T();
        services.AddSingleton(created);
        return created;
    }
}
