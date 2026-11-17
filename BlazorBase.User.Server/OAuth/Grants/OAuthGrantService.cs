using BlazorBase.User.Models;
using BlazorBase.User.Server.AccessTokens.Caching;
using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.OAuth.Authorization;
using BlazorBase.User.Server.OAuth.Configuration;
using BlazorBase.User.Server.OAuth.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.OAuth.Grants;

/// <summary>
/// Persists grants and the authorization codes created with them, and revokes a grant together with
/// everything issued under it.
/// </summary>
public sealed class OAuthGrantService<TUser>(
    BaseUserDbContext<TUser> dbContext,
    IAccessTokenCache accessTokenCache,
    IOptionsMonitor<OAuthServerOptions> options,
    TimeProvider timeProvider) : IOAuthGrantService
    where TUser : BaseUser
{
    #region Injects
    private readonly BaseUserDbContext<TUser> DbContext = dbContext;
    private readonly IAccessTokenCache AccessTokenCache = accessTokenCache;
    private readonly IOptionsMonitor<OAuthServerOptions> Options = options;
    private readonly TimeProvider TimeProvider = timeProvider;
    #endregion

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OAuthGrantSummary>> ListAsync(string userId, CancellationToken cancellationToken = default)
    {
        var grants = await DbContext.OAuthGrants
            .AsNoTracking()
            .Where(grant => grant.UserId == userId && grant.RevokedAt == null)
            .Join(DbContext.OAuthClients, grant => grant.ClientId, client => client.Id, (grant, client) => new { Grant = grant, client.ClientName })
            .ToListAsync(cancellationToken);

        return grants
            .OrderByDescending(entry => entry.Grant.CreatedAt)
            .Select(entry => new OAuthGrantSummary
            {
                Id = entry.Grant.Id,
                ClientName = entry.ClientName,
                RedirectHost = OAuthRedirectHost.Describe(entry.Grant.RedirectUri),
                Scope = entry.Grant.Scope,
                CreatedAt = entry.Grant.CreatedAt,
                LastUsedAt = entry.Grant.LastUsedAt,
            })
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<bool> RevokeAsync(Guid grantId, string? userId = null, CancellationToken cancellationToken = default)
    {
        var exists = await DbContext.OAuthGrants
            .AnyAsync(grant => grant.Id == grantId && grant.RevokedAt == null && (userId == null || grant.UserId == userId), cancellationToken);

        if (!exists)
            return false;

        await RevokeGrantAsync(grantId, cancellationToken);
        return true;
    }

    /// <summary>
    /// Revokes <paramref name="grantId"/> unconditionally: marks the grant revoked, consumes its refresh
    /// tokens, revokes its access tokens in one bulk update and evicts them from the access-token cache.
    /// </summary>
    public async Task RevokeGrantAsync(Guid grantId, CancellationToken cancellationToken = default)
    {
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;

        await DbContext.OAuthGrants
            .Where(grant => grant.Id == grantId && grant.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(grant => grant.RevokedAt, utcNow), cancellationToken);

        await DbContext.OAuthRefreshTokens
            .Where(token => token.GrantId == grantId && token.ConsumedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.ConsumedAt, utcNow), cancellationToken);

        var accessTokenIds = await DbContext.AccessTokens
            .Where(token => token.OAuthGrantId == grantId)
            .Select(token => token.Id)
            .ToListAsync(cancellationToken);

        await DbContext.AccessTokens
            .Where(token => token.OAuthGrantId == grantId && !token.IsRevoked)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.IsRevoked, true), cancellationToken);

        foreach (var accessTokenId in accessTokenIds)
            AccessTokenCache.Remove(accessTokenId);
    }

    /// <summary>
    /// Records the user's approval of <paramref name="request"/> as a new grant and creates its
    /// single-use authorization code. Returns the plain code; only its hash is stored.
    /// </summary>
    public async Task<string> CreateAuthorizationAsync(OAuthAuthorizationRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        var code = OAuthSecretGenerator.CreateSecret();

        var grant = new OAuthGrant
        {
            Id = Guid.NewGuid(),
            ClientId = request.ClientId,
            UserId = userId,
            Scope = request.Scope,
            Resource = request.Resource,
            RedirectUri = request.RedirectUri,
            CreatedAt = utcNow,
        };

        DbContext.OAuthGrants.Add(grant);
        DbContext.OAuthAuthorizationCodes.Add(new OAuthAuthorizationCode
        {
            Id = Guid.NewGuid(),
            CodeHash = OAuthSecretGenerator.Hash(code),
            GrantId = grant.Id,
            RedirectUri = request.RedirectUriSupplied ? request.RedirectUri : null,
            CodeChallenge = request.CodeChallenge,
            ExpiresAt = utcNow + Options.CurrentValue.AuthorizationCodeLifetime,
        });

        await DbContext.SaveChangesAsync(cancellationToken);
        return code;
    }

    /// <summary>Returns <see langword="true"/> when the account exists and is active.</summary>
    public async Task<bool> IsUserActiveAsync(string userId, CancellationToken cancellationToken = default) =>
        await DbContext.Users.AnyAsync(user => user.Id == userId && user.IsActive, cancellationToken);
}
