namespace BlazorBase.User.Models;

/// <summary>
/// The result of <c>IAuthService.ChangePasswordAsync</c>: either the password was changed, or the server
/// refused with a <see cref="Code"/> from <see cref="ChangePasswordErrorCodes"/> and, for a rejected new
/// password, the reasons per field in <see cref="FieldErrors"/> (keys <c>currentPassword</c> and
/// <c>newPassword</c>).
/// </summary>
public sealed class ChangePasswordOutcome
{
    public bool Succeeded { get; init; }

    /// <summary>One of <see cref="ChangePasswordErrorCodes"/> when the server refused; otherwise <see langword="null"/>.</summary>
    public string? Code { get; init; }

    /// <summary>The server's English message for the refusal; a page shows its own localized text for a known <see cref="Code"/>.</summary>
    public string? Message { get; init; }

    /// <summary>The reasons per field, keyed by the camelCase property name of <see cref="ChangePasswordRequest"/>.</summary>
    public IReadOnlyDictionary<string, string[]> FieldErrors { get; init; } = new Dictionary<string, string[]>();

    public static ChangePasswordOutcome Success() => new() { Succeeded = true };

    public static ChangePasswordOutcome Rejected(string? code, string? message, IReadOnlyDictionary<string, string[]>? fieldErrors = null) =>
        new() { Code = code, Message = message, FieldErrors = fieldErrors ?? new Dictionary<string, string[]>() };
}
