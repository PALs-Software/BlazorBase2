namespace BlazorBase.User.Server.AccessTokens.Caching;

/// <summary>
/// An in-memory projection of an <see cref="Entities.AccessToken"/> held in the prefix-keyed cache,
/// carrying only the fields needed to find and hash-match a candidate.
/// </summary>
/// <remarks>
/// <c>IsRevoked</c> and <c>ExpiresAt</c> are deliberately absent. Both are mutable by a process
/// other than the one holding this cache - another instance behind the load balancer, or an
/// operator CLI - so a cached copy is stale by definition, and a cached <c>IsRevoked = false</c>
/// would keep a revoked token working until the cache happened to be rebuilt.
/// <see cref="AccessTokenService{TContext}"/> therefore re-reads both from the database on every
/// successful hash match, which is one indexed primary-key lookup per authenticated request.
/// </remarks>
public record CachedAccessToken(Guid Id, string Prefix, string TokenHash, string Name, string? UserId);
