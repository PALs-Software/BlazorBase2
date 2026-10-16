using BlazorBase.Notifications.Server.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.Notifications.Server.Data;

/// <summary>
/// EF Core model builder extension that includes <see cref="PushSubscription"/> in the host's
/// model. Indexes and column constraints are expressed via Data Annotations on
/// <see cref="PushSubscription"/> itself; this extension handles only what annotations cannot
/// express.
/// </summary>
public static class PushSubscriptionEntityConfiguration
{
    /// <summary>
    /// Registers <see cref="PushSubscription"/> in the model. Call this from the host's
    /// <c>OnModelCreating</c> override (or equivalent) before generating a migration.
    /// </summary>
    /// <param name="modelBuilder">The model builder instance.</param>
    public static ModelBuilder ApplyPushSubscriptionConfiguration(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PushSubscription>();
        return modelBuilder;
    }
}
