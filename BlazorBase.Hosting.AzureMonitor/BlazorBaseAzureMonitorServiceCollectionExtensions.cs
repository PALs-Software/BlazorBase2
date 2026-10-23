using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.Hosting.AzureMonitor;

/// <summary>
/// Opt-in Azure Monitor (Application Insights) telemetry through OpenTelemetry. Lives in its own
/// package so <c>BlazorBase.Hosting</c> carries no Azure SDK dependency.
/// </summary>
public static class BlazorBaseAzureMonitorServiceCollectionExtensions
{
    /// <summary>The configuration key (and environment variable) App Service sets for Application Insights.</summary>
    public const string DefaultConnectionStringKey = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    /// <summary>
    /// Enables Azure Monitor OpenTelemetry when <paramref name="connectionStringKey"/> has a value in
    /// <paramref name="configuration"/>. Does nothing at all otherwise, so local development and sites
    /// without Application Insights stay untouched.
    /// </summary>
    /// <param name="configure">Further Azure Monitor settings, for example a <c>Credential</c> for Entra-only ingestion.</param>
    public static IServiceCollection AddBlazorBaseAzureMonitor(this IServiceCollection services, IConfiguration configuration, string connectionStringKey = DefaultConnectionStringKey, Action<AzureMonitorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringKey);

        var connectionString = configuration[connectionStringKey];
        if (string.IsNullOrWhiteSpace(connectionString))
            return services;

        services.AddOpenTelemetry().UseAzureMonitor(options =>
        {
            options.ConnectionString = connectionString;
            configure?.Invoke(options);
        });

        return services;
    }
}
