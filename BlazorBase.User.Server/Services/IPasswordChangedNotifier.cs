using BlazorBase.User.Server.Entities;

namespace BlazorBase.User.Server.Services;

/// <summary>
/// Tells the owner of an account that its password was changed. Called by <c>POST change-password</c>
/// after the change succeeded; the endpoint catches and logs any exception, so an implementation may
/// throw without failing the request. The default <see cref="NoOpPasswordChangedNotifier{TUser}"/> sends
/// nothing; <c>AddBlazorBaseMailingEmailSender</c> replaces it with
/// <see cref="MailingPasswordChangedNotifier{TUser}"/>.
/// </summary>
public interface IPasswordChangedNotifier<TUser>
    where TUser : BaseUser
{
    /// <summary>Notifies <paramref name="user"/> at <paramref name="email"/>.</summary>
    Task NotifyPasswordChangedAsync(TUser user, string email, CancellationToken cancellationToken = default);
}
