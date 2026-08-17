namespace BlazorBase.Files.Models;

/// <summary>
/// Result returned by the server after a successful file upload. Contains all metadata the client
/// needs to reference the stored file and render a preview or thumbnail.
/// </summary>
/// <param name="Id">The stable persistent identifier assigned to the stored file.</param>
/// <param name="FileName">Original file name without extension.</param>
/// <param name="Extension">File extension including the leading dot.</param>
/// <param name="ContentType">MIME content type.</param>
/// <param name="FileSize">File size in bytes.</param>
/// <param name="Hash">SHA-256 hex digest used for cache-busting, or <see langword="null"/> if not computed.</param>
public sealed record FileUploadResult(
    Guid Id,
    string FileName,
    string Extension,
    string ContentType,
    long FileSize,
    string? Hash);
