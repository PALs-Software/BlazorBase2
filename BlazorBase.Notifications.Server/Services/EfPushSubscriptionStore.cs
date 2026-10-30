using BlazorBase.Notifications.Server.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.Notifications.Server.Services;

/// <summary>
/// <see cref="IPushSubscriptionStore"/> implementation that works against any host
/// <see cref="DbContext"/> exposing <c>DbSet&lt;PushSubscription&gt;</c>, resolved the same way
/// <c>BlazorBase.Files.Server</c> resolves the host context: the base <see cref="DbContext"/> type
/// is bridged to the host's concrete, scoped registration by
/// <see cref="BlazorBaseNotificationsServerServiceCollectionExtensions.AddBlazorBaseNotificationsServer{TContext}"/>.
/// </summary>
public class EfPushSubscriptionStore(DbContext dbContext) : IPushSubscriptionStore
{
    #region Injects
    private readonly DbContext DbContext = dbContext;
    #endregion

    /// <inheritdoc/>
    public async Task UpsertAsync(Guid userId, string endpoint, string p256dh, string auth, string? userAgent, CancellationToken cancellationToken = default)
    {
        var subscription = await DbContext.Set<PushSubscription>()
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            DbContext.Set<PushSubscription>().Add(new PushSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth,
                UserAgent = userAgent,
                Enabled = true,
            });
        }
        else
        {
            subscription.UserId = userId;
            subscription.P256dh = p256dh;
            subscription.Auth = auth;
            subscription.UserAgent = userAgent;
            subscription.Enabled = true;
        }

        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveByEndpointAsync(Guid userId, string endpoint, CancellationToken cancellationToken = default)
    {
        var subscription = await DbContext.Set<PushSubscription>()
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint && s.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
            return false;

        DbContext.Set<PushSubscription>().Remove(subscription);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var subscription = await DbContext.Set<PushSubscription>()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
            return;

        DbContext.Set<PushSubscription>().Remove(subscription);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> SetEnabledAsync(Guid userId, string endpoint, bool enabled, CancellationToken cancellationToken = default)
    {
        var subscription = await DbContext.Set<PushSubscription>()
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint && s.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
            return false;

        subscription.Enabled = enabled;
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PushSubscription>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await DbContext.Set<PushSubscription>()
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PushSubscription>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await DbContext.Set<PushSubscription>()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
