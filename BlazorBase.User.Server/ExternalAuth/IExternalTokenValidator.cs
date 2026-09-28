using System.Security.Claims;

namespace BlazorBase.User.Server.ExternalAuth;

/// <summary>
/// Validates an externally issued OIDC token's signature, audience and lifetime, and returns the
/// resulting claims principal. Returns <see langword="null"/> for any token that fails validation
/// for any reason - an invalid signature, an unexpected audience, an expired lifetime, or a
/// malformed token - so a caller never has to distinguish the failure mode.
/// </summary>
public interface IExternalTokenValidator
{
    Task<ClaimsPrincipal?> ValidateAsync(string token, CancellationToken cancellationToken);
}
