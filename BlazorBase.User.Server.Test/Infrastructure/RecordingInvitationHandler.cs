using System.Security.Claims;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.Invitations;

namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>
/// An <see cref="IUserInvitationHandler{TUser}"/> whose answers a test sets, recording every call. Registered
/// as a singleton, so the test reads the same instance the service used.
/// </summary>
public sealed class RecordingInvitationHandler : IUserInvitationHandler<TestUser>
{
    public string? CreatingRejection { get; set; }

    public string? AcceptedRejection { get; set; }

    public string? ContextToSet { get; set; }

    /// <summary>When set, only invitations with this context may be managed.</summary>
    public string? ManageableContext { get; set; }

    public List<UserInvitation> CreatingCalls { get; } = [];

    public List<(TestUser User, UserInvitation Invitation)> AcceptedCalls { get; } = [];

    public Task<UserInvitationHandlerResult> OnCreatingAsync(UserInvitation invitation, ClaimsPrincipal invitedBy, CancellationToken cancellationToken)
    {
        CreatingCalls.Add(invitation);

        if (ContextToSet is not null)
            invitation.Context = ContextToSet;

        return Task.FromResult(CreatingRejection is null ? UserInvitationHandlerResult.Success : UserInvitationHandlerResult.Reject(CreatingRejection));
    }

    public Task<bool> CanManageAsync(UserInvitation invitation, ClaimsPrincipal administrator, CancellationToken cancellationToken) =>
        Task.FromResult(ManageableContext is null || invitation.Context == ManageableContext);

    public Task<UserInvitationHandlerResult> OnAcceptedAsync(TestUser user, UserInvitation invitation, CancellationToken cancellationToken)
    {
        AcceptedCalls.Add((user, invitation));
        return Task.FromResult(AcceptedRejection is null ? UserInvitationHandlerResult.Success : UserInvitationHandlerResult.Reject(AcceptedRejection));
    }
}
