namespace BlazorBase.User.Models;

/// <summary>
/// The machine-readable <c>code</c> member of the problem details the invitation endpoints answer
/// failures with, so a client can show its own localized message.
/// </summary>
public static class UserInvitationErrorCodes
{
    /// <summary>The request failed validation; <c>errors</c> holds the messages per field.</summary>
    public const string ValidationFailed = "validation_failed";

    /// <summary>The invitation does not exist or the caller may not manage it.</summary>
    public const string NotFound = "invitation_not_found";

    /// <summary>The token is unknown, expired, revoked or already used.</summary>
    public const string InvalidToken = "invitation_invalid";

    /// <summary>An account with the invited email address already exists.</summary>
    public const string UserExists = "user_exists";

    /// <summary>The role does not exist.</summary>
    public const string UnknownRole = "role_unknown";

    /// <summary>The invitation was accepted or revoked and can no longer be changed.</summary>
    public const string NotPending = "invitation_not_pending";

    /// <summary>The password does not satisfy the password policy; <c>errors</c> holds the reasons.</summary>
    public const string PasswordRejected = "password_rejected";

    /// <summary>An application <c>IUserInvitationHandler</c> refused; <c>detail</c> holds its message.</summary>
    public const string Rejected = "invitation_rejected";
}
