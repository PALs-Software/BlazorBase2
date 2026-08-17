namespace BlazorBase.Files.Server.Services;

/// <summary>
/// Represents the result of persisting a file to temporary or permanent storage.
/// </summary>
/// <param name="FileId">The stable identifier used to locate the file on disk.</param>
/// <param name="FilePath">Absolute path to the stored file.</param>
/// <param name="Hash">SHA-256 hex digest of the file content.</param>
/// <param name="FileSize">Number of bytes written.</param>
public sealed record StoredFile(Guid FileId, string FilePath, string Hash, long FileSize);
