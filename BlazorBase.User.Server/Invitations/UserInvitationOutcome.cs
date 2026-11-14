namespace BlazorBase.User.Server.Invitations;

/// <summary>How an operation of <see cref="UserInvitationService{TUser}"/> ended; maps onto the HTTP status.</summary>
public enum UserInvitationOutcome
{
    Succeeded,
    Invalid,
    NotFound,
    Conflict,
    Rejected,
}
