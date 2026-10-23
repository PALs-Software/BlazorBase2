using Microsoft.EntityFrameworkCore;

namespace BlazorBase.Dashboard.Server;

/// <summary>
/// EF Core model builder extension that includes <see cref="SavedDashboardLayout"/> in the host's model.
/// </summary>
public static class SavedDashboardLayoutEntityConfiguration
{
    /// <summary>
    /// Registers <see cref="SavedDashboardLayout"/> in the model. The table name and the unique index are expressed through
    /// annotations on the entity itself. Call this from the host's <c>OnModelCreating</c> override before generating a migration.
    /// </summary>
    public static ModelBuilder ApplySavedDashboardLayoutConfiguration(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SavedDashboardLayout>();
        return modelBuilder;
    }

    /// <summary>
    /// Registers <see cref="SavedDashboardLayout"/> and links it to the host's user table with a cascading foreign key
    /// (<see cref="SavedDashboardLayout.UserId"/> to the key of <typeparamref name="TUser"/>), so deleting a user also deletes
    /// their layouts. <typeparamref name="TUser"/> must be the host's user entity with a string key, for example an
    /// ASP.NET Core Identity user. Hosts without a user table use the non-generic overload and call
    /// <see cref="IDashboardLayoutRepository.DeleteAllForUserAsync"/> when a user is removed.
    /// </summary>
    public static ModelBuilder ApplySavedDashboardLayoutConfiguration<TUser>(this ModelBuilder modelBuilder)
        where TUser : class
    {
        modelBuilder.Entity<SavedDashboardLayout>()
            .HasOne<TUser>()
            .WithMany()
            .HasForeignKey(layout => layout.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        return modelBuilder;
    }
}
