using BlazorBase.User.Server.Entities;

namespace BlazorBase.User.Server.AccessTokens;

/// <summary>
/// The result of a successful token validation. <c>UserId</c> is <see langword="null"/> for tokens
/// that are not bound to an account. <see cref="Entity"/> is the authoritative row read back from
/// the database during validation - a host that minted a TPH-derived <see cref="AccessToken"/> type
/// via <see cref="IAccessTokenService.CreateAsync{TToken}"/> can pattern-match it back here to reach
/// its own bindings.
/// </summary>
public record ValidatedAccessToken(Guid TokenId, string Name, string? UserId, AccessToken Entity);
