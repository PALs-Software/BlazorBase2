using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.Components.Services;

public static class BlazorBaseComponentsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the services the shared layout and its controls resolve.
    /// </summary>
    /// <remarks>
    /// Call it from any host that renders <c>BaseLayout</c>, <c>BaseThemeToggle</c> or the navigation
    /// components. <c>AddBlazorBaseUserClient</c> already does, so an application built on the user
    /// stack needs nothing extra; one that only wants the shell would otherwise have had to reference
    /// the auth package purely to get these registrations, which is what this project exists to avoid.
    ///
    /// Registration is additive-safe: calling it twice, or after the user stack, leaves a single
    /// registration for each service.
    /// </remarks>
    public static IServiceCollection AddBlazorBaseComponents(this IServiceCollection services)
    {
        services.TryAddScopedService<IThemeService, ThemeService>();
        services.TryAddScopedService<ILanguageService, LanguageService>();

        return services;
    }

    private static void TryAddScopedService<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(TService)))
            return;

        services.AddScoped<TService, TImplementation>();
    }
}
