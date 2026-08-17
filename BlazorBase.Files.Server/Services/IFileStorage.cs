namespace BlazorBase.Files.Server.Services;

/// <summary>
/// Abstracts filesystem storage for uploaded files. Implementations handle the
/// temporary-to-permanent commit flow, SHA-256 hashing, and path-traversal-safe naming
/// (on-disk filenames are derived only from validated <see cref="Guid"/> values and stored
/// extensions — never from client-supplied filenames).
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Writes <paramref name="content"/> to the temporary store and returns a <see cref="StoredFile"/>
    /// describing the saved file. A new <see cref="Guid"/> is generated as the temporary file identifier.
    /// </summary>
    /// <param name="content">Readable stream of the file bytes.</param>
    /// <param name="extension">File extension including the leading dot (e.g. <c>.jpg</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<StoredFile> SaveTemporaryAsync(Stream content, string extension, CancellationToken cancellationToken);

    /// <summary>
    /// Moves a file from the temporary store to the permanent store and creates a thumbnail
    /// entry side-file when <paramref name="thumbnailStream"/> is supplied.
    /// </summary>
    /// <param name="permanentFileId">The stable <see cref="Guid"/> assigned to the committed file.</param>
    /// <param name="tempFileId">The temporary identifier returned by <see cref="SaveTemporaryAsync"/>.</param>
    /// <param name="extension">Stored file extension including the leading dot.</param>
    /// <param name="thumbnailStream">Optional thumbnail image stream; when non-null it is written to the thumbnail side-file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CommitAsync(Guid permanentFileId, Guid tempFileId, string extension, Stream? thumbnailStream, CancellationToken cancellationToken);

    /// <summary>
    /// Opens the permanent file identified by <paramref name="fileId"/> for reading.
    /// Returns <see langword="null"/> if the file does not exist on disk.
    /// </summary>
    /// <param name="fileId">Permanent file identifier.</param>
    /// <param name="thumbnail">When <see langword="true"/>, returns the thumbnail side-file instead of the original.</param>
    /// <param name="extension">File extension including the leading dot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<FileReadResult?> OpenReadAsync(Guid fileId, bool thumbnail, string extension, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the permanent file (and its thumbnail, if present) from the store.
    /// No-ops silently when the file does not exist.
    /// </summary>
    /// <param name="fileId">Permanent file identifier.</param>
    /// <param name="extension">File extension including the leading dot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(Guid fileId, string extension, CancellationToken cancellationToken);

    /// <summary>
    /// Computes the SHA-256 hex digest of <paramref name="content"/>.
    /// The stream position is reset to the beginning before reading and left at the end.
    /// </summary>
    string ComputeSha256(Stream content);
}
