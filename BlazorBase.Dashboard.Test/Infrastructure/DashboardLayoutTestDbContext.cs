using BlazorBase.Dashboard.Server;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.Dashboard.Test.Infrastructure;

/// <summary>
/// A test context holding only the dashboard layout table.
/// </summary>
public sealed class DashboardLayoutTestDbContext(DbContextOptions<DashboardLayoutTestDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplySavedDashboardLayoutConfiguration();
    }
}
