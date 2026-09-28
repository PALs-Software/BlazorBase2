using System.Security.Claims;
using BlazorBase.User.Server.Entities;

namespace BlazorBase.User.Server.ExternalAuth;

/// <summary>
/// Seam for resolving a validated external identity to an application user. The host decides what
/// "resolve" means - by subject id, by email reconciliation, or any other rule - and may reject an
/// unknown subject outright via <see cref="ExternalUserResolution{TUser}.Reject"/> instead of
/// auto-provisioning one. There is no default implementation: whether a subject exists is a
/// host-specific authorization decision, not a framework default.
/// </summary>
public interface IExternalUserResolver<TUser> where TUser : BaseUser
{
    Task<ExternalUserResolution<TUser>> ResolveAsync(ClaimsPrincipal externalPrincipal, CancellationToken cancellationToken);
}
