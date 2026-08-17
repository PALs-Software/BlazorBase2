namespace BlazorBase.CRUD.Models;

/// <summary>
/// Thrown by <see cref="Events.IBaseDataInterceptor{TModel}"/> implementations to signal a
/// business-rule violation that should be shown to the user as a 400 Bad Request response.
/// The <see cref="Exception.Message"/> is the user-facing validation message.
/// </summary>
public sealed class BaseValidationException : Exception
{
    /// <summary>Initializes a new instance with the specified user-facing validation message.</summary>
    /// <param name="message">The validation message to display to the user.</param>
    public BaseValidationException(string message) : base(message) { }

    /// <summary>Initializes a new instance with the specified user-facing validation message and a reference to the inner exception that is the cause of this exception.</summary>
    /// <param name="message">The validation message to display to the user.</param>
    /// <param name="inner">The exception that is the cause of this exception.</param>
    public BaseValidationException(string message, Exception inner) : base(message, inner) { }
}
