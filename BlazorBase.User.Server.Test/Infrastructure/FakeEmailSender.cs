using BlazorBase.User.Server.Entities;
using Microsoft.AspNetCore.Identity;

namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>
/// Captures every call instead of sending anything, so a test can assert what would have been sent -
/// and prove that nothing ever reaches a real mail server.
/// </summary>
public sealed class FakeEmailSender<TUser> : IEmailSender<TUser>
    where TUser : BaseUser
{
    public List<(string Email, string Link)> ConfirmationLinks { get; } = [];
    public List<(string Email, string Link)> PasswordResetLinks { get; } = [];

    public Task SendConfirmationLinkAsync(TUser user, string email, string confirmationLink)
    {
        ConfirmationLinks.Add((email, confirmationLink));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(TUser user, string email, string resetLink)
    {
        PasswordResetLinks.Add((email, resetLink));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(TUser user, string email, string resetCode) => Task.CompletedTask;
}
