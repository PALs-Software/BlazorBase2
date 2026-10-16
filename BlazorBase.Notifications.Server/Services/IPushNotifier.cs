namespace BlazorBase.Notifications.Server.Services;

/// <summary>Delivers Web Push messages to one user's devices or to every registered device.</summary>
public interface IPushNotifier
{
    /// <summary>
    /// Sends <paramref name="payload"/> to every enabled subscription belonging to
    /// <paramref name="userId"/>. A no-op if the user has none.
    /// </summary>
    Task NotifyUserAsync(Guid userId, PushPayload payload, CancellationToken cancellationToken = default);

    /// <summary>Sends <paramref name="payload"/> to every enabled subscription across every user.</summary>
    Task NotifyAllAsync(PushPayload payload, CancellationToken cancellationToken = default);
}
