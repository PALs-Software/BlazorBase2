namespace BlazorBase.CRUD.Sanitization;

/// <summary>
/// Abstraction for sanitizing an HTML string.
/// Returns a non-null result; returns an empty string when the input is null.
/// Implement this interface in the consuming application — no concrete implementation is provided here.
/// </summary>
public interface IHtmlSanitizer
{
    /// <summary>
    /// Sanitizes the given HTML string and returns a safe result.
    /// </summary>
    /// <param name="html">Raw HTML input, or null.</param>
    /// <returns>Sanitized HTML, never null. Empty string when <paramref name="html"/> is null.</returns>
    string Sanitize(string? html);
}
