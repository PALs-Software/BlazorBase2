using BlazorBase.User.Server.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace BlazorBase.User.Server.Services;

/// <summary>
/// Logs a warning and does nothing else. The default <see cref="IEmailSender{TUser}"/>, so a host
/// that has not configured mail still starts - email being unreachable must not be a startup failure,
/// but silently discarding a confirmation or reset link is worth a log line.
/// </summary>
public class NoOpEmailSender<TUser>(ILogger<NoOpEmailSender<TUser>> logger) : IEmailSender<TUser>
    where TUser : BaseUser
{
    #region Injects
    private readonly ILogger<NoOpEmailSender<TUser>> Logger = logger;
    #endregion

    public Task SendConfirmationLinkAsync(TUser user, string email, string confirmationLink) =>
        LogAndReturn(email, "confirmation link");

    public Task SendPasswordResetLinkAsync(TUser user, string email, string resetLink) =>
        LogAndReturn(email, "password reset link");

    public Task SendPasswordResetCodeAsync(TUser user, string email, string resetCode) =>
        LogAndReturn(email, "password reset code");

    private Task LogAndReturn(string email, string kind)
    {
        Logger.LogWarning("No email sender is configured; the {Kind} for {Email} was not sent.", kind, email);
        return Task.CompletedTask;
    }
}
