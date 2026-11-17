using BlazorBase.User.Server.AccessTokens.Caching;
using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.OAuth.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.OAuth.Maintenance;

/// <summary>
/// Periodically removes OAuth rows that can no longer be used: authorization codes expired more than a
/// day ago, refresh tokens expired or consumed more than a day ago, OAuth access tokens expired more
/// than a day ago, grants revoked more than <see cref="RevokedGrantRetention"/> ago, and clients older
/// than a day that never obtained a grant (abandoned dynamic registrations).
/// </summary>
/// <remarks>
/// The day of slack keeps a just-consumed refresh token around long enough for reuse detection to
/// recognise it, and a revoked grant stays visible for a month for diagnosis. Children are deleted
/// explicitly before their grant rather than relying on the database cascade, so the sweep behaves the
/// same on a provider or connection that does not enforce foreign keys.
/// </remarks>
public class OAuthCleanupService<TUser>(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<OAuthServerOptions> options,
    TimeProvider timeProvider,
    ILogger<OAuthCleanupService<TUser>> logger) : BackgroundService
    where TUser : BaseUser
{
    #region Injects
    private readonly IServiceScopeFactory ScopeFactory = scopeFactory;
    private readonly IOptionsMonitor<OAuthServerOptions> Options = options;
    private readonly TimeProvider TimeProvider = timeProvider;
    private readonly ILogger<OAuthCleanupService<TUser>> Logger = logger;
    #endregion

    /// <summary>How long expired or consumed rows are kept before they are deleted.</summary>
    public static readonly TimeSpan ExpiredRowRetention = TimeSpan.FromDays(1);

    /// <summary>How long a revoked grant is kept before it is deleted.</summary>
    public static readonly TimeSpan RevokedGrantRetention = TimeSpan.FromDays(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Options.CurrentValue.CleanupInterval, TimeProvider);

        do
        {
            await RunCleanupAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// Runs one sweep. Exposed so a test can exercise a single pass without the timer. A failure is
    /// logged and swallowed, so a transient database error never takes the host down.
    /// </summary>
    public async Task RunCleanupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = ScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BaseUserDbContext<TUser>>();
            var accessTokenCache = scope.ServiceProvider.GetService<IAccessTokenCache>();
            var utcNow = TimeProvider.GetUtcNow().UtcDateTime;
            var expiredBefore = utcNow - ExpiredRowRetention;
            var revokedBefore = utcNow - RevokedGrantRetention;

            await dbContext.OAuthAuthorizationCodes
                .Where(code => code.ExpiresAt < expiredBefore)
                .ExecuteDeleteAsync(cancellationToken);

            await dbContext.OAuthRefreshTokens
                .Where(token => token.ExpiresAt < expiredBefore || token.ConsumedAt < expiredBefore)
                .ExecuteDeleteAsync(cancellationToken);

            var expiredAccessTokenIds = await dbContext.AccessTokens
                .Where(token => token.OAuthGrantId != null && token.ExpiresAt < expiredBefore)
                .Select(token => token.Id)
                .ToListAsync(cancellationToken);

            await dbContext.AccessTokens
                .Where(token => expiredAccessTokenIds.Contains(token.Id))
                .ExecuteDeleteAsync(cancellationToken);

            foreach (var accessTokenId in expiredAccessTokenIds)
                accessTokenCache?.Remove(accessTokenId);

            var revokedGrants = dbContext.OAuthGrants.Where(grant => grant.RevokedAt < revokedBefore);

            await dbContext.OAuthAuthorizationCodes
                .Where(code => revokedGrants.Any(grant => grant.Id == code.GrantId))
                .ExecuteDeleteAsync(cancellationToken);

            await dbContext.OAuthRefreshTokens
                .Where(token => revokedGrants.Any(grant => grant.Id == token.GrantId))
                .ExecuteDeleteAsync(cancellationToken);

            await revokedGrants.ExecuteDeleteAsync(cancellationToken);

            await dbContext.OAuthClients
                .Where(client => client.CreatedAt < expiredBefore && !dbContext.OAuthGrants.Any(grant => grant.ClientId == client.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogError(exception, "OAuth cleanup pass failed; will retry on the next interval.");
        }
    }
}
