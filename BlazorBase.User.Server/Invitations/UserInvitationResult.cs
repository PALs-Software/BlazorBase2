namespace BlazorBase.User.Server.Invitations;

/// <summary>
/// The result of an operation of <see cref="UserInvitationService{TUser}"/>: the value on success, otherwise
/// the outcome, a <see cref="Models.UserInvitationErrorCodes"/> code, a message and optional field errors.
/// </summary>
public sealed class UserInvitationResult<TValue>
{
    private UserInvitationResult(UserInvitationOutcome outcome, TValue? value, string? errorCode, string? errorMessage, IDictionary<string, string[]>? errors)
    {
        Outcome = outcome;
        Value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        Errors = errors;
    }

    public UserInvitationOutcome Outcome { get; }

    public bool Succeeded => Outcome == UserInvitationOutcome.Succeeded;

    public TValue? Value { get; }

    public string? ErrorCode { get; }

    public string? ErrorMessage { get; }

    /// <summary>Messages per request field, for a validation failure.</summary>
    public IDictionary<string, string[]>? Errors { get; }

    public static UserInvitationResult<TValue> Success(TValue value) => new(UserInvitationOutcome.Succeeded, value, null, null, null);

    public static UserInvitationResult<TValue> Failure(UserInvitationOutcome outcome, string errorCode, string errorMessage, IDictionary<string, string[]>? errors = null) =>
        new(outcome, default, errorCode, errorMessage, errors);
}
