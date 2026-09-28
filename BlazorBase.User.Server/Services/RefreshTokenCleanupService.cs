using BlazorBase.User.Server.Configuration;
using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorBase.User.Server.Services;

/// <summary>
/// Periodically deletes refresh-token rows that are expired or revoked - the deleted original table's
/// second defect, unbounded growth, because nothing ever removed a row once it stopped being useful.
/// </summary>
public class RefreshTokenCleanupService<TUser>(
    IServiceScopeFactory scopeFactory,
    RefreshTokenCleanupOptions options,
    TimeProvider timeProvider,
    ILogger<RefreshTokenCleanupService<TUser>> logger) : BackgroundService
    where TUser : BaseUser
{
    #region Injects
    private readonly IServiceScopeFactory ScopeFactory = scopeFactory;
    private readonly RefreshTokenCleanupOptions Options = options;
    private readonly TimeProvider TimeProvider = timeProvider;
    private readonly ILogger<RefreshTokenCleanupService<TUser>> Logger = logger;
    #endregion

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Options.Interval, TimeProvider);

        do
        {
            await RunCleanupAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// Deletes every revoked or expired row in one pass. Exposed so a test can exercise a single pass
    /// without waiting on the periodic timer. A failure here - a transient database blip - is logged
    /// and swallowed rather than propagated, so a cleanup hiccup can never take the host down with it.
    /// </summary>
    public async Task RunCleanupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = ScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BaseUserDbContext<TUser>>();
            var utcNow = TimeProvider.GetUtcNow().UtcDateTime;

            await dbContext.RefreshTokens
                .Where(token => token.IsRevoked || token.ExpiresAt < utcNow)
                .ExecuteDeleteAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogError(exception, "Refresh-token cleanup pass failed; will retry on the next interval.");
        }
    }
}
