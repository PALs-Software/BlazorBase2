using System.Security.Claims;

namespace BlazorBase.Notifications.Server.Services;

/// <summary>
/// Default <see cref="INotificationUserIdResolver"/>: reads the
/// <see cref="ClaimTypes.NameIdentifier"/> claim as a <see cref="Guid"/>. Registered by
/// <c>AddBlazorBaseNotificationsServer</c> unless a host already registered its own
/// <see cref="INotificationUserIdResolver"/>.
/// </summary>
public sealed class ClaimsNotificationUserIdResolver : INotificationUserIdResolver
{
    public Task<Guid?> ResolveUserIdAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var resolved = Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : (Guid?)null;
        return Task.FromResult(resolved);
    }
}
