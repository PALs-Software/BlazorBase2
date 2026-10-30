using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorBase.Components.Diagnostics;

/// <summary>Registration of the client diagnostics buffer and its recorders.</summary>
public static class ClientDiagnosticsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IClientDiagnostics"/>, the <see cref="ClientDiagnosticsOptions"/> and the
    /// <see cref="ClientDiagnosticsHandler"/>. The buffer is one instance for the whole process in the browser and one
    /// per scope elsewhere, so a Blazor Server circuit never sees the errors of another user, unless
    /// <paramref name="lifetime"/> says otherwise. In the browser the logger provider
    /// <see cref="ClientDiagnosticsLoggerProvider"/> is added as well. Calling it again only adds configuration.
    /// </summary>
    public static IServiceCollection AddBlazorBaseClientDiagnostics(
        this IServiceCollection services,
        Action<ClientDiagnosticsOptions>? configure = null,
        ServiceLifetime? lifetime = null)
    {
        var options = services.AddOptions<ClientDiagnosticsOptions>();
        if (configure is not null)
            options.Configure(configure);

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IClientDiagnostics)))
        {
            var effective = lifetime ?? (OperatingSystem.IsBrowser() ? ServiceLifetime.Singleton : ServiceLifetime.Scoped);
            services.Add(new ServiceDescriptor(typeof(IClientDiagnostics), typeof(ClientDiagnostics), effective));
            if (effective == ServiceLifetime.Singleton)
            {
                services.AddSingleton<ILoggerProvider>(provider =>
                    new ClientDiagnosticsLoggerProvider(
                        provider.GetRequiredService<IClientDiagnostics>(),
                        provider.GetRequiredService<IOptions<ClientDiagnosticsOptions>>().Value));
            }
        }

        services.TryAddTransient<ClientDiagnosticsHandler>();

        return services;
    }
}
