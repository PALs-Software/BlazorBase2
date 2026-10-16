using BlazorBase.Notifications.Models;
using BlazorBase.Notifications.Server.Services;

namespace BlazorBase.Notifications.Server.Test.TestDoubles;

/// <summary>
/// In-memory <see cref="INotificationInboxStore"/> test double. Records every call so a test can
/// assert which user id and notification id the endpoint mapper actually passed through.
/// </summary>
public sealed class FakeNotificationInboxStore : INotificationInboxStore
{
    public Dictionary<Guid, List<InboxNotificationDto>> UnreadByUser { get; } = [];

    public List<Guid> GetUnreadCalls { get; } = [];

    public List<Guid> GetUnreadCountCalls { get; } = [];

    public List<(Guid UserId, long Id)> MarkReadCalls { get; } = [];

    public List<(Guid UserId, long Id)> DismissCalls { get; } = [];

    public Task<IReadOnlyList<InboxNotificationDto>> GetUnreadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        GetUnreadCalls.Add(userId);
        IReadOnlyList<InboxNotificationDto> result = UnreadByUser.TryGetValue(userId, out var notifications) ? notifications : [];
        return Task.FromResult(result);
    }

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        GetUnreadCountCalls.Add(userId);
        var count = UnreadByUser.TryGetValue(userId, out var notifications) ? notifications.Count : 0;
        return Task.FromResult(count);
    }

    public Task MarkReadAsync(Guid userId, long id, CancellationToken cancellationToken = default)
    {
        MarkReadCalls.Add((userId, id));
        return Task.CompletedTask;
    }

    public Task DismissAsync(Guid userId, long id, CancellationToken cancellationToken = default)
    {
        DismissCalls.Add((userId, id));
        return Task.CompletedTask;
    }
}
