using BlazorBase.User.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>Concrete identity DbContext over <see cref="TestUser"/> for the server-side tests.</summary>
public sealed class TestUserDbContext(DbContextOptions options) : BaseUserDbContext<TestUser>(options)
{
    /// <summary>Registers <see cref="TestDerivedAccessToken"/> as a TPH-derived <c>AccessToken</c>,
    /// so tests can exercise <c>IAccessTokenService.CreateAsync{TToken}</c> against a real model.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<TestDerivedAccessToken>();
    }
}

