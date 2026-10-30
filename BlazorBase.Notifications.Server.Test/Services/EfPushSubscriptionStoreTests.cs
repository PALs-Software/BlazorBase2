using BlazorBase.Notifications.Server.Entities;
using BlazorBase.Notifications.Server.Services;
using BlazorBase.Notifications.Server.Test.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorBase.Notifications.Server.Test.Services;

public sealed class EfPushSubscriptionStoreTests : IDisposable
{
    private readonly SqliteConnection Connection;
    private readonly TestNotificationsDbContext DbContext;
    private readonly EfPushSubscriptionStore Store;

    public EfPushSubscriptionStoreTests()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var options = new DbContextOptionsBuilder<TestNotificationsDbContext>()
            .UseSqlite(Connection)
            .Options;

        DbContext = new TestNotificationsDbContext(options);
        DbContext.Database.EnsureCreated();

        Store = new EfPushSubscriptionStore(DbContext);
    }

    [Fact]
    public async Task UpsertAsync_CreatesANewSubscription_ForAnUnknownEndpoint()
    {
        var userId = Guid.NewGuid();

        await Store.UpsertAsync(userId, "https://push.example.test/new", "p256dh", "auth", "Chrome on Windows", CancellationToken.None);

        var stored = await DbContext.PushSubscriptions.SingleAsync();
        Assert.Equal(userId, stored.UserId);
        Assert.Equal("https://push.example.test/new", stored.Endpoint);
        Assert.Equal("p256dh", stored.P256dh);
        Assert.Equal("auth", stored.Auth);
        Assert.Equal("Chrome on Windows", stored.UserAgent);
        Assert.True(stored.Enabled);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesInPlace_AndReassignsOwnership_WhenTheEndpointAlreadyExists()
    {
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        const string endpoint = "https://push.example.test/reused";

        await Store.UpsertAsync(firstUserId, endpoint, "p256dh-old", "auth-old", "Old device", CancellationToken.None);
        await Store.UpsertAsync(secondUserId, endpoint, "p256dh-new", "auth-new", "New device", CancellationToken.None);

        var stored = await DbContext.PushSubscriptions.SingleAsync();
        Assert.Equal(secondUserId, stored.UserId);
        Assert.Equal("p256dh-new", stored.P256dh);
        Assert.Equal("auth-new", stored.Auth);
        Assert.Equal("New device", stored.UserAgent);
    }

    [Fact]
    public async Task RemoveByEndpointAsync_RemovesTheSubscription_AndReturnsTrue_WhenItBelongsToTheCaller()
    {
        var userId = Guid.NewGuid();
        await Store.UpsertAsync(userId, "https://push.example.test/own", "p256dh", "auth", null, CancellationToken.None);

        var removed = await Store.RemoveByEndpointAsync(userId, "https://push.example.test/own", CancellationToken.None);

        Assert.True(removed);
        Assert.Empty(await DbContext.PushSubscriptions.ToListAsync());
    }

    [Fact]
    public async Task RemoveByEndpointAsync_ReturnsFalse_AndLeavesItInPlace_WhenItBelongsToAnotherUser()
    {
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        await Store.UpsertAsync(ownerId, "https://push.example.test/owned", "p256dh", "auth", null, CancellationToken.None);

        var removed = await Store.RemoveByEndpointAsync(attackerId, "https://push.example.test/owned", CancellationToken.None);

        Assert.False(removed);
        Assert.Single(await DbContext.PushSubscriptions.ToListAsync());
    }

    [Fact]
    public async Task RemoveByEndpointAsync_ReturnsFalse_ForAnUnknownEndpoint()
    {
        var removed = await Store.RemoveByEndpointAsync(Guid.NewGuid(), "https://push.example.test/missing", CancellationToken.None);

        Assert.False(removed);
    }

    [Fact]
    public async Task RemoveAsync_RemovesTheSubscriptionById_RegardlessOfOwner()
    {
        var userId = Guid.NewGuid();
        await Store.UpsertAsync(userId, "https://push.example.test/to-remove", "p256dh", "auth", null, CancellationToken.None);
        var stored = await DbContext.PushSubscriptions.SingleAsync();

        await Store.RemoveAsync(stored.Id, CancellationToken.None);

        Assert.Empty(await DbContext.PushSubscriptions.ToListAsync());
    }

    [Fact]
    public async Task RemoveAsync_IsANoOp_ForAnUnknownId()
    {
        var exception = await Record.ExceptionAsync(() => Store.RemoveAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task SetEnabledAsync_UpdatesTheSubscription_AndReturnsTrue_WhenItBelongsToTheCaller()
    {
        var userId = Guid.NewGuid();
        await Store.UpsertAsync(userId, "https://push.example.test/toggle", "p256dh", "auth", null, CancellationToken.None);

        var updated = await Store.SetEnabledAsync(userId, "https://push.example.test/toggle", false, CancellationToken.None);

        Assert.True(updated);
        var stored = await DbContext.PushSubscriptions.SingleAsync();
        Assert.False(stored.Enabled);
    }

    [Fact]
    public async Task SetEnabledAsync_ReturnsFalse_AndLeavesItUnchanged_WhenItBelongsToAnotherUser()
    {
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        await Store.UpsertAsync(ownerId, "https://push.example.test/owned-toggle", "p256dh", "auth", null, CancellationToken.None);

        var updated = await Store.SetEnabledAsync(attackerId, "https://push.example.test/owned-toggle", false, CancellationToken.None);

        Assert.False(updated);
        var stored = await DbContext.PushSubscriptions.SingleAsync();
        Assert.True(stored.Enabled);
    }

    [Fact]
    public async Task GetForUserAsync_ReturnsOnlyThatUsersSubscriptions()
    {
        var userId = Guid.NewGuid();
        await Store.UpsertAsync(userId, "https://push.example.test/mine-1", "p256dh", "auth", null, CancellationToken.None);
        await Store.UpsertAsync(userId, "https://push.example.test/mine-2", "p256dh", "auth", null, CancellationToken.None);
        await Store.UpsertAsync(Guid.NewGuid(), "https://push.example.test/not-mine", "p256dh", "auth", null, CancellationToken.None);

        var subscriptions = await Store.GetForUserAsync(userId, CancellationToken.None);

        Assert.Equal(2, subscriptions.Count);
        Assert.All(subscriptions, subscription => Assert.Equal(userId, subscription.UserId));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEverySubscription_AcrossEveryUser()
    {
        await Store.UpsertAsync(Guid.NewGuid(), "https://push.example.test/all-1", "p256dh", "auth", null, CancellationToken.None);
        await Store.UpsertAsync(Guid.NewGuid(), "https://push.example.test/all-2", "p256dh", "auth", null, CancellationToken.None);

        var subscriptions = await Store.GetAllAsync(CancellationToken.None);

        Assert.Equal(2, subscriptions.Count);
    }

    public void Dispose()
    {
        DbContext.Dispose();
        Connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
