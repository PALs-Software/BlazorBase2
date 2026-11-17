namespace BlazorBase.User.Server.Invitations;

/// <summary>The answer of an <see cref="IUserInvitationHandler{TUser}"/>: go ahead, or refuse with a message.</summary>
public sealed class UserInvitationHandlerResult
{
    private UserInvitationHandlerResult(bool succeeded, string? errorMessage)
    {
        Succeeded = succeeded;
        ErrorMessage = errorMessage;
    }

    public bool Succeeded { get; }

    /// <summary>Shown to the caller as the problem detail when <see cref="Succeeded"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; }

    public static UserInvitationHandlerResult Success { get; } = new(true, null);

    public static UserInvitationHandlerResult Reject(string errorMessage) => new(false, errorMessage);
}
