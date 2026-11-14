namespace BlazorBase.User.Models;

/// <summary>
/// The machine-readable <c>code</c> member of the <c>400</c> bodies <c>POST change-password</c> answers
/// refusals with, so a client can show its own localized message next to the right field.
/// </summary>
public static class ChangePasswordErrorCodes
{
    /// <summary>The current password does not match. The server deliberately says nothing more.</summary>
    public const string CurrentPasswordIncorrect = "current_password_incorrect";

    /// <summary>The account has no password (it signs in through an external identity); it can set one through "forgot password".</summary>
    public const string NoPasswordSet = "no_password_set";

    /// <summary>The request failed validation or the new password breaks the password rules; <c>errors</c> holds the messages per field.</summary>
    public const string ValidationFailed = "validation_failed";
}
