using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace BlazorBase.Hosting.Data;

/// <summary>
/// Opt-in SQL Server setup for Azure SQL. Hosts that keep calling <c>UseSqlServer</c> directly are unaffected.
/// </summary>
public static class BlazorBaseSqlServerExtensions
{
    /// <summary>
    /// Uses SQL Server with <paramref name="connectionString"/> and, when <paramref name="enableRetry"/> is
    /// <see langword="true"/>, <c>EnableRetryOnFailure()</c> so transient Azure SQL errors are retried.
    /// Every explicit transaction must then run inside <c>Database.CreateExecutionStrategy().ExecuteAsync(...)</c>;
    /// the transactions in the BlazorBase libraries do.
    /// </summary>
    public static DbContextOptionsBuilder UseBlazorBaseSqlServer(this DbContextOptionsBuilder optionsBuilder, string connectionString, bool enableRetry, Action<SqlServerDbContextOptionsBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return optionsBuilder.UseSqlServer(connectionString, sqlServerOptions =>
        {
            if (enableRetry)
                sqlServerOptions.EnableRetryOnFailure();

            configure?.Invoke(sqlServerOptions);
        });
    }
}
