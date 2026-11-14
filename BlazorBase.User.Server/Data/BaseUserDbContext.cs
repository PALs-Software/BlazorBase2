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
    /// Applications registered with the OAuth authorization server. Declared here, like
    /// <see cref="AccessTokens"/>, so a host that later calls <c>AddBlazorBaseOAuthServer</c> needs no
    /// model change; a host that never does only gets empty tables in its next migration.
    /// </summary>
    public DbSet<OAuthClient> OAuthClients => Set<OAuthClient>();

    /// <summary>The user approvals the OAuth authorization server issues tokens under.</summary>
    public DbSet<OAuthGrant> OAuthGrants => Set<OAuthGrant>();

    /// <summary>Hashed, single-use OAuth authorization codes.</summary>
    public DbSet<OAuthAuthorizationCode> OAuthAuthorizationCodes => Set<OAuthAuthorizationCode>();

    /// <summary>Hashed, rotating OAuth refresh tokens.</summary>
    public DbSet<OAuthRefreshToken> OAuthRefreshTokens => Set<OAuthRefreshToken>();

    /// <summary>
    /// Invitations created through <c>MapBlazorBaseUserInvitationEndpoints</c>. Declared here, like
    /// <see cref="AccessTokens"/>, so a host that later calls <c>AddBlazorBaseUserInvitations</c> needs no
    /// model change - only a migration.
    /// </summary>
    public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();

    /// <summary>
    /// A real foreign key with cascade delete, so a deleted user cannot leave orphaned refresh-token
    /// rows behind - the deleted table's original defect. The OAuth tables cascade the same way: a
    /// deleted user or client takes its grants, and a deleted grant its codes and refresh tokens.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RefreshToken>()
            .HasOne<TUser>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OAuthGrant>()
            .HasOne<TUser>()
            .WithMany()
            .HasForeignKey(grant => grant.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OAuthGrant>()
            .HasOne<OAuthClient>()
            .WithMany()
            .HasForeignKey(grant => grant.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OAuthAuthorizationCode>()
            .HasOne<OAuthGrant>()
            .WithMany()
            .HasForeignKey(code => code.GrantId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OAuthRefreshToken>()
            .HasOne<OAuthGrant>()
            .WithMany()
            .HasForeignKey(token => token.GrantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Without this an int key becomes IDENTITY by convention, the database picks the value,
        // and the fixed key that makes the row a singleton - the whole setup race guard - is silently ignored.
        modelBuilder.Entity<SetupCompletionMarker>()
            .Property(marker => marker.Id)
            .ValueGeneratedNever();
    }
}
