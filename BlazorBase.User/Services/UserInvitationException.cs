using System.Net;

namespace BlazorBase.User.Services;

/// <summary>
/// A refusal of the invitation API: the problem details' <c>code</c> (a
/// <see cref="Models.UserInvitationErrorCodes"/> value), <c>detail</c> as <see cref="Exception.Message"/> and
/// the field messages of <c>errors</c>, flattened.
/// </summary>
public sealed class UserInvitationException(string? errorCode, string message, IReadOnlyList<string> errors, HttpStatusCode statusCode)
    : HttpRequestException(message, inner: null, statusCode)
{
    public string? ErrorCode { get; } = errorCode;

    public IReadOnlyList<string> Errors { get; } = errors;
}
