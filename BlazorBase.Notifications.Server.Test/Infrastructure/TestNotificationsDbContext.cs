using BlazorBase.Notifications.Server.Data;
using BlazorBase.Notifications.Server.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.Notifications.Server.Test.Infrastructure;

/// <summary>Minimal <see cref="DbContext"/> exposing <see cref="PushSubscription"/> for the server-side tests.</summary>
public sealed class TestNotificationsDbContext(DbContextOptions<TestNotificationsDbContext> options) : DbContext(options)
{
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyPushSubscriptionConfiguration();
}
