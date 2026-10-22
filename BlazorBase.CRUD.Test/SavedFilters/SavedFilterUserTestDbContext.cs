using BlazorBase.CRUD.SavedFilters.Server;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.CRUD.Test.SavedFilters;

/// <summary>
/// A host-like user entity with a string key, standing in for an ASP.NET Core Identity user.
/// </summary>
public sealed class SavedFilterTestUser
{
    public string Id { get; set; } = String.Empty;
}

/// <summary>
/// The saved-filter model with the cascading foreign key to <see cref="SavedFilterTestUser"/>.
/// </summary>
public sealed class SavedFilterUserTestDbContext(DbContextOptions<SavedFilterUserTestDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<SavedFilterTestUser>().HasKey(user => user.Id);
        modelBuilder.ApplySavedListFilterConfiguration<SavedFilterTestUser>();
    }
}
