using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.Dashboard.Test.Infrastructure;

/// <summary>
/// A real in-memory SQLite database with the dashboard layout model, handing out a fresh context per call like the host's
/// context factory does.
/// </summary>
public sealed class DashboardLayoutTestDatabase : IDbContextFactory<DashboardLayoutTestDbContext>, IDisposable
{
    private readonly SqliteConnection Connection = new("DataSource=:memory:");
    private readonly DbContextOptions<DashboardLayoutTestDbContext> Options;

    public DashboardLayoutTestDatabase()
    {
        Connection.Open();
        Options = new DbContextOptionsBuilder<DashboardLayoutTestDbContext>().UseSqlite(Connection).Options;

        using var context = CreateDbContext();
        context.Database.EnsureCreated();
    }

    public DashboardLayoutTestDbContext CreateDbContext() => new(Options);

    public void Dispose() => Connection.Dispose();
}
