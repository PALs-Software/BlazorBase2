using BlazorBase.User.Server.AccessTokens.Caching;
using BlazorBase.User.Server.AccessTokens.Generation;
using BlazorBase.User.Server.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.User.Server.AccessTokens;

/// <summary>
/// Scoped service implementing the full access-token lifecycle against <typeparamref name="TContext"/>.
/// </summary>
/// <typeparam name="TContext">
/// Any <see cref="DbContext"/> whose model contains <see cref="AccessToken"/> - which every
/// <see cref="Data.BaseUserDbContext{TUser}"/> does.
/// </typeparam>
/// <remarks>
/// Validation is deliberately database-authoritative rather than cache-authoritative. The cache
/// answers only "which rows share this prefix, and what is their hash" - facts that never change
/// for an existing token. Whether the token is still valid <i>right now</i> is re-read from the
/// database on every successful hash match. That is what makes revocation take effect immediately
/// across processes: a second instance behind the load balancer, or an operator revoking through a
/// CLI, changes a row this service always re-reads instead of a cache it cannot reach. The cost is
/// one indexed primary-key lookup per authenticated request; the alternative is a window, bounded
/// only by process lifetime, in which a revoked token still works.
/// </remarks>
public class AccessTokenService<TContext>(
    TContext dbContext,
    IAccessTokenSecretGenerator secretGenerator,
    IAccessTokenCache cache)
    : IAccessTokenService
    where TContext : DbContext
{
    #region Injects
    private readonly TContext DbContext = dbContext;
    private readonly IAccessTokenSecretGenerator SecretGenerator = secretGenerator;
    private readonly IAccessTokenCache Cache = cache;
    #endregion

    private static readonly TimeSpan TouchThrottleWindow = TimeSpan.FromSeconds(60);

    private DbSet<AccessToken> AccessTokens => DbContext.Set<AccessToken>();

    public async Task<CreatedAccessToken> CreateAsync(string name, DateTime? expiresAt, string? userId = null, CancellationToken cancellationToken = default)
    {
        var generated = SecretGenerator.Generate();

        var entity = new AccessToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Prefix = generated.Prefix,
            TokenHash = generated.Hash,
            Name = name,
            ExpiresAt = expiresAt,
            IsRevoked = false
        };

        AccessTokens.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        Cache.Set(new CachedAccessToken(entity.Id, entity.Prefix, entity.TokenHash, entity.Name, entity.UserId));

        return new CreatedAccessToken(entity.Id, entity.Name, generated.Secret, entity.ExpiresAt);
    }

    public async Task<ValidatedAccessToken?> ValidateAsync(string secret, CancellationToken cancellationToken = default)
    {
        var prefix = SecretGenerator.ExtractPrefix(secret);

        if (!Cache.TryGetByPrefix(prefix, out var candidates))
            candidates = await LoadCandidatesFromDatabaseAsync(prefix, cancellationToken);

        foreach (var candidate in candidates)
        {
            if (!SecretGenerator.MatchesHash(secret, candidate.TokenHash))
                continue;

            return await VerifyCurrentStateAsync(candidate, cancellationToken);
        }

        return null;
    }

    public async Task<bool> RevokeAsync(Guid tokenId, string? userId = null, CancellationToken cancellationToken = default)
    {
        var entity = await AccessTokens
            .FirstOrDefaultAsync(
                accessToken => accessToken.Id == tokenId && (userId == null || accessToken.UserId == userId),
                cancellationToken);

        if (entity is null)
            return false;

        entity.IsRevoked = true;
        await DbContext.SaveChangesAsync(cancellationToken);

        Cache.Remove(tokenId);
        return true;
    }

    public async Task<IReadOnlyList<AccessTokenSummary>> ListAsync(string? userId = null, CancellationToken cancellationToken = default)
    {
        var entities = await AccessTokens
            .AsNoTracking()
            .Where(accessToken => userId == null || accessToken.UserId == userId)
            .OrderBy(accessToken => accessToken.Name)
            .ToListAsync(cancellationToken);

        return entities
            .Select(entity => new AccessTokenSummary(entity.Id, entity.Name, entity.UserId, entity.ExpiresAt, entity.LastUsedAt, entity.IsRevoked))
            .ToList();
    }

    public async Task TouchAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        var entity = await AccessTokens
            .FirstOrDefaultAsync(accessToken => accessToken.Id == tokenId, cancellationToken);

        if (entity is null)
            return;

        if (entity.LastUsedAt.HasValue && DateTime.UtcNow - entity.LastUsedAt.Value < TouchThrottleWindow)
            return;

        entity.LastUsedAt = DateTime.UtcNow;
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Loads every row sharing <paramref name="prefix"/> straight from the database (an indexed
    /// lookup) and caches each one for next time. Reading through on a miss, instead of relying on
    /// a one-time preload, is what makes a token created by another process work immediately rather
    /// than only after this one restarts.
    /// </summary>
    private async Task<IReadOnlyList<CachedAccessToken>> LoadCandidatesFromDatabaseAsync(string prefix, CancellationToken cancellationToken)
    {
        var entities = await AccessTokens
            .AsNoTracking()
            .Where(accessToken => accessToken.Prefix == prefix)
            .ToListAsync(cancellationToken);

        var candidates = entities
            .Select(entity => new CachedAccessToken(entity.Id, entity.Prefix, entity.TokenHash, entity.Name, entity.UserId))
            .ToList();

        foreach (var candidate in candidates)
            Cache.Set(candidate);

        return candidates;
    }

    /// <summary>
    /// Re-reads <c>IsRevoked</c>/<c>ExpiresAt</c> straight from the database for a hash-matched
    /// candidate - never from the cache - so a revocation made by another process takes effect on
    /// the very next request.
    /// </summary>
    private async Task<ValidatedAccessToken?> VerifyCurrentStateAsync(CachedAccessToken candidate, CancellationToken cancellationToken)
    {
        var currentState = await AccessTokens
            .AsNoTracking()
            .Where(accessToken => accessToken.Id == candidate.Id)
            .Select(accessToken => new { accessToken.IsRevoked, accessToken.ExpiresAt, accessToken.UserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (currentState is null || currentState.IsRevoked)
            return null;

        if (currentState.ExpiresAt.HasValue && currentState.ExpiresAt.Value < DateTime.UtcNow)
            return null;

        return new ValidatedAccessToken(candidate.Id, candidate.Name, currentState.UserId);
    }
}
