using BlazorBase.CRUD.SavedFilters.Server;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.CRUD.Test.SavedFilters;

public sealed class SavedFilterTestDbContext(DbContextOptions<SavedFilterTestDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplySavedListFilterConfiguration();
    }
}
