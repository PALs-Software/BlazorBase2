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

    /// <summary>
    /// Registers <see cref="SavedListFilter"/> and links it to the host's user table with a cascading foreign key
    /// (<see cref="SavedListFilter.UserId"/> to the key of <typeparamref name="TUser"/>), so deleting a user also deletes
    /// their saved filters. <typeparamref name="TUser"/> must be the host's user entity with a string key, for example an
    /// ASP.NET Core Identity user. Hosts without a user table use the non-generic overload and call
    /// <see cref="IListFilterRepository.DeleteAllForUserAsync"/> when a user is removed.
    /// </summary>
    public static ModelBuilder ApplySavedListFilterConfiguration<TUser>(this ModelBuilder modelBuilder)
        where TUser : class
    {
        modelBuilder.Entity<SavedListFilter>()
            .HasOne<TUser>()
            .WithMany()
            .HasForeignKey(filter => filter.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        return modelBuilder;
    }
}
