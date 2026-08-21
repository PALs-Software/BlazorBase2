namespace BlazorBase.Components.Services;

/// <summary>
/// Asks the user to confirm an action and reports whether they accepted. The default
/// implementation shows a FluentUI confirmation dialog; tests can substitute a stub.
/// </summary>
public interface IConfirmationService
{
    /// <summary>
    /// Shows a confirmation prompt and returns <c>true</c> when the user accepts,
    /// <c>false</c> when they decline or dismiss it.
    /// </summary>
    Task<bool> ConfirmAsync(string message, string title, string confirmButtonText, string cancelButtonText);
}
