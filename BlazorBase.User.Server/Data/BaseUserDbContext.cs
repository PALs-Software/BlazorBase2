using BlazorBase.User.Server.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.User.Server.Data;

public abstract class BaseUserDbContext<TUser>(DbContextOptions options) : IdentityDbContext<TUser>(options)
    where TUser : BaseUser
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// Long-lived, hashed credentials for programmatic access. Declaring the set here means every
    /// deriving context carries the table, so an application that later calls
    /// <c>AddBlazorBaseAccessTokens</c> needs no model change - only a migration.
    /// </summary>
    public DbSet<AccessToken> AccessTokens => Set<AccessToken>();
}
