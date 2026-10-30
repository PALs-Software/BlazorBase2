using System.Security.Claims;
using BlazorBase.Notifications.Server.Services;

namespace BlazorBase.Notifications.Server.Test.TestDoubles;

/// <summary>
/// <see cref="INotificationUserIdResolver"/> test double that translates the identity user id
/// carried in the <see cref="ClaimTypes.NameIdentifier"/> claim to a different, host-chosen domain
/// user id — the shape a host with separate identity and domain user ids registers for real.
/// Returns <see langword="null"/> for an identity id with no mapping.
/// </summary>
public sealed class MappingNotificationUserIdResolver(IReadOnlyDictionary<Guid, Guid> identityToDomainUserId) : INotificationUserIdResolver
{
    public Task<Guid?> ResolveUserIdAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var identityUserId))
            return Task.FromResult<Guid?>(null);

        return Task.FromResult(identityToDomainUserId.TryGetValue(identityUserId, out var domainUserId) ? domainUserId : (Guid?)null);
    }
}
