using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BlazorBase.Hosting.Health;

/// <summary>
/// Opt-in health endpoints. Nothing is registered or mapped unless the host calls these methods.
/// </summary>
public static class BlazorBaseHealthChecksExtensions
{
    /// <summary>The default time a readiness check may take before it counts as unhealthy.</summary>
    public static readonly TimeSpan DefaultReadinessTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Registers a liveness check (the process responds) and a readiness check (<typeparamref name="TContext"/>
    /// can reach its database). Returns the <see cref="IHealthChecksBuilder"/> so other providers can add
    /// their own readiness checks with <see cref="AddBlazorBaseReadinessCheck{THealthCheck}"/>.
    /// </summary>
    public static IHealthChecksBuilder AddBlazorBaseHealthChecks<TContext>(this IServiceCollection services, TimeSpan? databaseTimeout = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), [BlazorBaseHealthCheckTags.Live])
            .Add(new HealthCheckRegistration(
                "database",
                serviceProvider => new DbContextReadinessHealthCheck<TContext>(serviceProvider.GetRequiredService<IServiceScopeFactory>()),
                HealthStatus.Unhealthy,
                [BlazorBaseHealthCheckTags.Ready],
                databaseTimeout ?? DefaultReadinessTimeout));
    }

    /// <summary>
    /// Registers <typeparamref name="THealthCheck"/> as a readiness check, so it is part of the
    /// <c>/ready</c> and aggregate endpoints. This is the extension point for storage or other providers.
    /// </summary>
    public static IHealthChecksBuilder AddBlazorBaseReadinessCheck<THealthCheck>(this IHealthChecksBuilder builder, string name, TimeSpan? timeout = null)
        where THealthCheck : class, IHealthCheck
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return builder.AddCheck<THealthCheck>(name, HealthStatus.Unhealthy, [BlazorBaseHealthCheckTags.Ready], timeout ?? DefaultReadinessTimeout);
    }

    /// <summary>
    /// Maps three anonymous endpoints: <paramref name="pattern"/> runs every registered check,
    /// <c>{pattern}/live</c> only the liveness checks, <c>{pattern}/ready</c> only the readiness checks.
    /// Each answers <c>200</c> with <c>Healthy</c> or <c>503</c> with <c>Unhealthy</c> / <c>Degraded</c>
    /// as plain text, without details, and is never cached.
    /// </summary>
    public static IEndpointConventionBuilder MapBlazorBaseHealthChecks(this IEndpointRouteBuilder endpoints, string pattern = "/health")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        var basePattern = pattern.TrimEnd('/');

        var composite = new CompositeEndpointConventionBuilder(
        [
            endpoints.MapHealthChecks(basePattern, new HealthCheckOptions { Predicate = _ => true }),
            endpoints.MapHealthChecks($"{basePattern}/live", new HealthCheckOptions { Predicate = registration => registration.Tags.Contains(BlazorBaseHealthCheckTags.Live) }),
            endpoints.MapHealthChecks($"{basePattern}/ready", new HealthCheckOptions { Predicate = registration => registration.Tags.Contains(BlazorBaseHealthCheckTags.Ready) }),
        ]);

        composite.AllowAnonymous();
        return composite;
    }
}
