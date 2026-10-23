using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorBase.Hosting.BackgroundJobs;

/// <summary>
/// Opt-in switch for background jobs. Without a call to these methods every hosted service runs as before.
/// </summary>
public static class BlazorBaseBackgroundJobsServiceCollectionExtensions
{
    /// <summary>
    /// The recurring BlazorBase hosted services that <see cref="AddBlazorBaseBackgroundJobGate"/> gates. The
    /// e-mail sender is not among them: it drains a bounded queue, so stopping it would block every caller once the queue is full.
    /// </summary>
    public static readonly IReadOnlyList<string> GatedFrameworkHostedServiceTypeNames =
    [
        "BlazorBase.Files.Server.Services.TemporaryFileCleanupService",
        "BlazorBase.User.Server.Services.RefreshTokenCleanupService`1",
    ];

    /// <summary>
    /// Registers <see cref="IBackgroundJobGate"/>, bound to <c>BackgroundJobs:Enabled</c> (default
    /// <see langword="true"/>), and wraps the BlazorBase hosted services listed in
    /// <see cref="GatedFrameworkHostedServiceTypeNames"/> so they do not start while the gate is off.
    /// Call it after the <c>AddBlazorBase...</c> methods that register those services; hosted services
    /// registered later are not touched.
    /// </summary>
    public static IServiceCollection AddBlazorBaseBackgroundJobGate(this IServiceCollection services, IConfiguration configuration, string sectionName = BackgroundJobOptions.SectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        services.Configure<BackgroundJobOptions>(configuration.GetSection(sectionName));
        services.TryAddSingleton<IBackgroundJobGate, ConfigurationBackgroundJobGate>();

        for (var index = 0; index < services.Count; index++)
        {
            var descriptor = services[index];
            if (descriptor.ServiceType != typeof(IHostedService) || descriptor.IsKeyedService)
                continue;

            var implementationType = descriptor.ImplementationType;
            if (implementationType is null || !IsGatedFrameworkType(implementationType))
                continue;

            services[index] = CreateGatedDescriptor(implementationType.Name, serviceProvider => ActivatorUtilities.CreateInstance(serviceProvider, implementationType));
        }

        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="THostedService"/> so it starts only while <see cref="IBackgroundJobGate.IsEnabled"/> is
    /// <see langword="true"/>. For a host's own hosted services; use it instead of <c>AddHostedService</c>.
    /// </summary>
    public static IServiceCollection AddGatedHostedService<THostedService>(this IServiceCollection services)
        where THostedService : class, IHostedService
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IBackgroundJobGate, ConfigurationBackgroundJobGate>();
        services.Add(CreateGatedDescriptor(typeof(THostedService).Name, serviceProvider => ActivatorUtilities.CreateInstance<THostedService>(serviceProvider)));
        return services;
    }

    private static ServiceDescriptor CreateGatedDescriptor(string name, Func<IServiceProvider, object> innerFactory) =>
        ServiceDescriptor.Singleton<IHostedService>(serviceProvider => new GatedHostedService(
            name,
            () => (IHostedService)innerFactory(serviceProvider),
            serviceProvider.GetRequiredService<IBackgroundJobGate>(),
            serviceProvider.GetRequiredService<ILogger<GatedHostedService>>()));

    private static bool IsGatedFrameworkType(Type implementationType)
    {
        for (var type = implementationType; type is not null; type = type.BaseType)
        {
            var name = (type.IsGenericType ? type.GetGenericTypeDefinition() : type).FullName;
            if (name is not null && GatedFrameworkHostedServiceTypeNames.Contains(name))
                return true;
        }

        return false;
    }
}
