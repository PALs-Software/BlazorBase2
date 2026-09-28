using BlazorBase.User.Server.Configuration;
using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BlazorBase.User.Server.Test.Services;

/// <summary>
/// Proves <see cref="RefreshTokenCleanupService{TUser}"/> sweeps only the rows it should: a single
/// pass, run directly rather than through the periodic timer, removes an expired row and a revoked
/// one, and leaves a live one alone.
/// </summary>
public sealed class RefreshTokenCleanupServiceTests
{
    private static ServiceProvider BuildProvider(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));

        // The same bridge AddBlazorBaseUserServer registers, so the cleanup service - which only
        // knows the non-generic base context - resolves the concrete one here as it does in a real host.
        services.AddScoped<BaseUserDbContext<TestUser>>(provider => provider.GetRequiredService<TestUserDbContext>());

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task RunCleanupAsync_RemovesExpiredAndRevokedRows_AndLeavesALiveOneAlone()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using var provider = BuildProvider(connection);

        Guid liveId;

        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TestUserDbContext>();
            await context.Database.EnsureCreatedAsync();

            var user = new TestUser { UserName = "ada@example.com", Email = "ada@example.com" };
            context.Users.Add(user);

            liveId = Guid.NewGuid();

            context.RefreshTokens.AddRange(
                new RefreshToken
                {
                    Id = Guid.NewGuid(),
                    Token = "expired-hash",
                    UserId = user.Id,
                    FamilyId = Guid.NewGuid(),
                    SecurityStamp = "stamp",
                    ExpiresAt = DateTime.UtcNow.AddDays(-1),
                    CreatedAt = DateTime.UtcNow.AddDays(-31),
                },
                new RefreshToken
                {
                    Id = Guid.NewGuid(),
                    Token = "revoked-hash",
                    UserId = user.Id,
                    FamilyId = Guid.NewGuid(),
                    SecurityStamp = "stamp",
                    ExpiresAt = DateTime.UtcNow.AddDays(29),
                    CreatedAt = DateTime.UtcNow,
                    IsRevoked = true,
                },
                new RefreshToken
                {
                    Id = liveId,
                    Token = "live-hash",
                    UserId = user.Id,
                    FamilyId = Guid.NewGuid(),
                    SecurityStamp = "stamp",
                    ExpiresAt = DateTime.UtcNow.AddDays(29),
                    CreatedAt = DateTime.UtcNow,
                });

            await context.SaveChangesAsync();
        }

        var cleanupService = new RefreshTokenCleanupService<TestUser>(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new RefreshTokenCleanupOptions(),
            TimeProvider.System,
            NullLogger<RefreshTokenCleanupService<TestUser>>.Instance);

        await cleanupService.RunCleanupAsync();

        using var verifyScope = provider.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TestUserDbContext>();
        var remaining = await verifyContext.RefreshTokens.ToListAsync();

        Assert.Single(remaining);
        Assert.Equal(liveId, remaining[0].Id);
    }
}
