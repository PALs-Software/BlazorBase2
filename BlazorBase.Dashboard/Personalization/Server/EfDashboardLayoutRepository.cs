using Microsoft.EntityFrameworkCore;

namespace BlazorBase.Dashboard.Server;

/// <summary>
/// EF Core implementation of <see cref="IDashboardLayoutRepository"/> over <see cref="SavedDashboardLayout"/>. Every query is
/// restricted to the given user, so another user's rows are neither visible nor reachable.
/// </summary>
public sealed class EfDashboardLayoutRepository<TContext>(IDbContextFactory<TContext> contextFactory) : IDashboardLayoutRepository
    where TContext : DbContext
{
    #region Injects

    private readonly IDbContextFactory<TContext> ContextFactory = contextFactory;

    #endregion

    /// <inheritdoc />
    public async Task<DashboardLayout> GetAsync(string userId, string dashboardKey, CancellationToken cancellationToken = default)
    {
        var key = DashboardLayoutValidator.ValidateKey(dashboardKey);
        await using var context = await ContextFactory.CreateDbContextAsync(cancellationToken);

        var json = await context.Set<SavedDashboardLayout>()
            .AsNoTracking()
            .Where(row => row.UserId == userId && row.DashboardKey == key)
            .Select(row => row.LayoutJson)
            .FirstOrDefaultAsync(cancellationToken);

        return DashboardLayoutSerializer.Deserialize(json);
    }

    /// <inheritdoc />
    public async Task SaveAsync(string userId, string dashboardKey, DashboardLayout layout, CancellationToken cancellationToken = default)
    {
        var key = DashboardLayoutValidator.ValidateKey(dashboardKey);
        var json = DashboardLayoutSerializer.Serialize(DashboardLayoutValidator.Normalize(layout));

        try
        {
            await UpsertAsync(userId, key, json, cancellationToken);
        }
        catch (DbUpdateException)
        {
            await UpsertAsync(userId, key, json, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string userId, string dashboardKey, CancellationToken cancellationToken = default)
    {
        var key = DashboardLayoutValidator.ValidateKey(dashboardKey);
        await using var context = await ContextFactory.CreateDbContextAsync(cancellationToken);

        var rows = await context.Set<SavedDashboardLayout>()
            .Where(row => row.UserId == userId && row.DashboardKey == key)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return false;

        context.Set<SavedDashboardLayout>().RemoveRange(rows);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<int> DeleteAllForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrEmpty(userId))
            return 0;

        await using var context = await ContextFactory.CreateDbContextAsync(cancellationToken);

        var rows = await context.Set<SavedDashboardLayout>()
            .Where(row => row.UserId == userId)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return 0;

        context.Set<SavedDashboardLayout>().RemoveRange(rows);
        await context.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    private async Task UpsertAsync(string userId, string key, string json, CancellationToken cancellationToken)
    {
        await using var context = await ContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = context.Set<SavedDashboardLayout>();
        var now = DateTime.UtcNow;

        var existing = await rows.FirstOrDefaultAsync(row => row.UserId == userId && row.DashboardKey == key, cancellationToken);

        if (existing is null)
            rows.Add(new SavedDashboardLayout { Id = Guid.NewGuid(), UserId = userId, DashboardKey = key, LayoutJson = json, CreatedOn = now, ModifiedOn = now });
        else
        {
            existing.LayoutJson = json;
            existing.ModifiedOn = now;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
