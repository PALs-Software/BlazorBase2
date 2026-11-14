namespace BlazorBase.User.Models;

/// <summary>Where an invitation stands, derived from its timestamps when it is listed.</summary>
public enum UserInvitationStatus
{
    /// <summary>Sent and still usable: not accepted, not revoked, not expired.</summary>
    Pending,

    /// <summary>Redeemed; the account exists.</summary>
    Accepted,

    /// <summary>Not redeemed before its expiry. A resend or a new link makes it pending again.</summary>
    Expired,

    /// <summary>Withdrawn by an administrator or replaced by a newer invitation for the same address.</summary>
    Revoked,
}
