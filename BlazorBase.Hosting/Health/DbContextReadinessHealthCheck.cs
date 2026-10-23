using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BlazorBase.Hosting.Health;

/// <summary>
/// Readiness check that opens a connection through <typeparamref name="TContext"/>. Prefers the
/// registered <see cref="IDbContextFactory{TContext}"/> (how BlazorBase servers register their context)
/// and falls back to a scoped <typeparamref name="TContext"/>.
/// </summary>
public sealed class DbContextReadinessHealthCheck<TContext>(IServiceScopeFactory scopeFactory) : IHealthCheck
    where TContext : DbContext
{
    #region Injects
    private readonly IServiceScopeFactory ScopeFactory = scopeFactory;
    #endregion

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = ScopeFactory.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetService<IDbContextFactory<TContext>>();

            if (factory is null)
                return await CheckAsync(scope.ServiceProvider.GetRequiredService<TContext>(), cancellationToken);

            await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
            return await CheckAsync(dbContext, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("The database is not reachable.", exception);
        }
    }

    private static async Task<HealthCheckResult> CheckAsync(TContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.Database.CanConnectAsync(cancellationToken))
            return HealthCheckResult.Healthy();

        return HealthCheckResult.Unhealthy("The database is not reachable.");
    }
}
