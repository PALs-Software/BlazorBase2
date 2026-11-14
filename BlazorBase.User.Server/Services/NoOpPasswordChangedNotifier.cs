using BlazorBase.User.Server.Entities;
using Microsoft.Extensions.Logging;

namespace BlazorBase.User.Server.Services;

/// <summary>
/// Logs at information level and does nothing else. The default <see cref="IPasswordChangedNotifier{TUser}"/>,
/// so a host that has not configured mail still changes passwords; a missing notification is not an
/// error, unlike a missing confirmation or reset link.
/// </summary>
public class NoOpPasswordChangedNotifier<TUser>(ILogger<NoOpPasswordChangedNotifier<TUser>> logger) : IPasswordChangedNotifier<TUser>
    where TUser : BaseUser
{
    #region Injects
    private readonly ILogger<NoOpPasswordChangedNotifier<TUser>> Logger = logger;
    #endregion

    public Task NotifyPasswordChangedAsync(TUser user, string email, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("No email sender is configured; the password changed notification for user {UserId} was not sent.", user.Id);
        return Task.CompletedTask;
    }
}
