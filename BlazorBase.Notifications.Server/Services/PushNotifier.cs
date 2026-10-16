using BlazorBase.Notifications.Server.Entities;
using Microsoft.Extensions.Logging;

namespace BlazorBase.Notifications.Server.Services;

/// <summary>
/// Default <see cref="IPushNotifier"/>: loads the relevant <see cref="PushSubscription"/> rows,
/// sends to every one that is <see cref="PushSubscription.Enabled"/> with bounded parallelism, and
/// removes a subscription the push service reports as gone. A failing or throwing send for one
/// subscription never stops delivery to the others in the same batch.
/// </summary>
public class PushNotifier(IPushSubscriptionStore store, IWebPushSender sender, ILogger<PushNotifier> logger) : IPushNotifier
{
    #region Injects
    private readonly IPushSubscriptionStore Store = store;
    private readonly IWebPushSender Sender = sender;
    private readonly ILogger<PushNotifier> Logger = logger;
    #endregion

    /// <summary>
    /// Caps how many sends run at once per <see cref="NotifyUserAsync"/>/<see cref="NotifyAllAsync"/>
    /// call, so a broadcast to many devices does not open an unbounded number of outbound requests.
    /// </summary>
    private const int MaxDegreeOfParallelism = 8;

    /// <inheritdoc/>
    public Task NotifyUserAsync(Guid userId, PushPayload payload, CancellationToken cancellationToken = default) =>
        NotifySubscriptionsAsync(() => Store.GetForUserAsync(userId, cancellationToken), payload, cancellationToken);

    /// <inheritdoc/>
    public Task NotifyAllAsync(PushPayload payload, CancellationToken cancellationToken = default) =>
        NotifySubscriptionsAsync(() => Store.GetAllAsync(cancellationToken), payload, cancellationToken);

    private async Task NotifySubscriptionsAsync(
        Func<Task<IReadOnlyList<PushSubscription>>> loadSubscriptions,
        PushPayload payload,
        CancellationToken cancellationToken)
    {
        var subscriptions = await loadSubscriptions().ConfigureAwait(false);
        var enabledSubscriptions = subscriptions.Where(subscription => subscription.Enabled).ToList();

        if (enabledSubscriptions.Count == 0)
            return;

        using var semaphore = new SemaphoreSlim(MaxDegreeOfParallelism);
        var sendTasks = enabledSubscriptions.Select(subscription => SendOneAsync(subscription, payload, semaphore, cancellationToken));
        await Task.WhenAll(sendTasks).ConfigureAwait(false);
    }

    private async Task SendOneAsync(PushSubscription subscription, PushPayload payload, SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await Sender.SendAsync(subscription, payload, cancellationToken).ConfigureAwait(false);

            if (result == PushSendResult.SubscriptionGone)
                await Store.RemoveAsync(subscription.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Push send to subscription {SubscriptionId} failed unexpectedly.", subscription.Id);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
