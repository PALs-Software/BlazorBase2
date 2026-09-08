using BlazorBase.User.Server.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.User.Server.Data;

public abstract class BaseUserDbContext<TUser>(DbContextOptions options) : IdentityDbContext<TUser>(options)
    where TUser : BaseUser
{
    /// <summary>
    /// Long-lived, hashed credentials for programmatic access. Declaring the set here means every
    /// deriving context carries the table, so an application that later calls
    /// <c>AddBlazorBaseAccessTokens</c> needs no model change - only a migration.
    /// </summary>
    public DbSet<AccessToken> AccessTokens => Set<AccessToken>();

    /// <summary>
    /// Hashed refresh-token rotation records backing <see cref="Services.AuthTokenIssuer{TUser}"/>.
    /// Declared here, not in a per-app context, for the same reason as <see cref="AccessTokens"/>.
    /// </summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// Backs the race-safe check in <see cref="Controllers.AuthControllerBase{TUser}.Setup"/> - see
    /// <see cref="SetupCompletionMarker"/> for why a single-row table, not the users table itself,
    /// is what closes the race.
    /// </summary>
    public DbSet<SetupCompletionMarker> SetupCompletionMarkers => Set<SetupCompletionMarker>();

    /// <summary>
    /// A real foreign key with cascade delete, so a deleted user cannot leave orphaned refresh-token
    /// rows behind - the deleted table's original defect.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RefreshToken>()
            .HasOne<TUser>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Without this an int key becomes IDENTITY by convention, the database picks the value,
        // and the fixed key that makes the row a singleton - the whole setup race guard - is silently ignored.
        modelBuilder.Entity<SetupCompletionMarker>()
            .Property(marker => marker.Id)
            .ValueGeneratedNever();
    }
}
