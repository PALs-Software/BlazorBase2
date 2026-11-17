using System.Security.Claims;
using BlazorBase.User.Server.Entities;

namespace BlazorBase.User.Server.Invitations;

/// <summary>
/// The application's hook into the invitation flow, registered with <c>AddUserInvitationHandler</c>.
/// Every member has a default that allows everything, so a handler implements only what it needs.
/// Several handlers run in registration order; the first refusal wins.
/// </summary>
/// <typeparam name="TUser">The host's user type.</typeparam>
public interface IUserInvitationHandler<TUser>
    where TUser : BaseUser
{
    /// <summary>
    /// Runs before a new invitation is stored. May validate or set <see cref="UserInvitation.Context"/>
    /// (for example to the inviting administrator's household) and may refuse the invitation.
    /// </summary>
    /// <param name="invitation">The invitation about to be stored; changes to it are saved.</param>
    /// <param name="invitedBy">The administrator creating the invitation.</param>
    Task<UserInvitationHandlerResult> OnCreatingAsync(UserInvitation invitation, ClaimsPrincipal invitedBy, CancellationToken cancellationToken) =>
        Task.FromResult(UserInvitationHandlerResult.Success);

    /// <summary>
    /// Whether <paramref name="administrator"/> may see and manage <paramref name="invitation"/>: list,
    /// resend, renew its link and revoke it. An invitation no handler allows answers <c>404</c>.
    /// </summary>
    Task<bool> CanManageAsync(UserInvitation invitation, ClaimsPrincipal administrator, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    /// <summary>
    /// Runs inside the accept transaction after the account was created and given its role, before the
    /// commit. Work done through the same scoped <c>DbContext</c> commits or rolls back with the account;
    /// a refusal rolls everything back and the invitation stays usable.
    /// </summary>
    /// <param name="user">The new account.</param>
    /// <param name="invitation">The invitation being redeemed, with <see cref="UserInvitation.AcceptedAt"/> and <see cref="UserInvitation.AcceptedUserId"/> set.</param>
    Task<UserInvitationHandlerResult> OnAcceptedAsync(TUser user, UserInvitation invitation, CancellationToken cancellationToken) =>
        Task.FromResult(UserInvitationHandlerResult.Success);
}
