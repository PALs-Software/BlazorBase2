using Microsoft.EntityFrameworkCore;

namespace BlazorBase.CRUD.Test.Endpoints;

/// <summary>
/// Multi-tenant style context: a global query filter hides every <see cref="HiddenRowTestEntity"/> whose
/// <see cref="HiddenRowTestEntity.Tenant"/> differs from the tenant the context was created for.
/// </summary>
public sealed class HiddenRowTestDbContext(
    DbContextOptions<HiddenRowTestDbContext> options,
    HiddenRowTenant tenant
) : DbContext(options)
{
    #region Injects
    private readonly HiddenRowTenant VisibleTenant = tenant;
    #endregion

    public DbSet<HiddenRowTestEntity> Entities => Set<HiddenRowTestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<HiddenRowTestEntity>()
            .HasQueryFilter(entity => entity.Tenant == VisibleTenant.Name);
    }
}
