using Microsoft.EntityFrameworkCore;

namespace BlazorBase.CRUD.SavedFilters.Server;

/// <summary>
/// EF Core model builder extension that includes <see cref="SavedListFilter"/> in the host's model.
/// </summary>
public static class SavedListFilterEntityConfiguration
{
    /// <summary>
    /// Registers <see cref="SavedListFilter"/> in the model. The table name and the unique index are expressed through
    /// annotations on the entity itself. Call this from the host's <c>OnModelCreating</c> override before generating a
    /// migration.
    /// </summary>
    public static ModelBuilder ApplySavedListFilterConfiguration(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SavedListFilter>();
        return modelBuilder;
    }
}
