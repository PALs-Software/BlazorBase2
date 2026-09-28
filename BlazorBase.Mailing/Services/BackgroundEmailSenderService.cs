using BlazorBase.Mailing.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorBase.Mailing.Services;

/// <summary>
/// Drains <see cref="IBackgroundEmailQueue"/> for the lifetime of the host and performs the actual
/// sends that <see cref="BackgroundEmailDispatcher"/> only queued. Resolves <see cref="IEmailSender"/>
/// from a fresh <see cref="IServiceScope"/> per message because the sender is registered scoped;
/// this service itself is a long-lived singleton and must not hold a single scope for its whole
/// lifetime.
/// </summary>
public class BackgroundEmailSenderService(
    IBackgroundEmailQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<BackgroundEmailSenderService> logger) : BackgroundService
{
    #region Injects
    private readonly IBackgroundEmailQueue Queue = queue;
    private readonly IServiceScopeFactory ScopeFactory = scopeFactory;
    private readonly ILogger<BackgroundEmailSenderService> Logger = logger;
    #endregion

    /// <summary>
    /// One failing message must not take the whole worker down, so each send is isolated in its
    /// own try/catch and the loop continues to the next queued message. Shutdown cancellation is
    /// let through unhandled rather than logged as a send failure.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in Queue.DequeueAllAsync(stoppingToken))
        {
            await using var scope = ScopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            try
            {
                await sender.SendAsync(message, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    exception,
                    "Failed to send background email with {RecipientCount} recipient(s) and subject '{Subject}'.",
                    message.To.Count + message.Cc.Count + message.Bcc.Count,
                    message.Subject);
            }
        }
    }
}
