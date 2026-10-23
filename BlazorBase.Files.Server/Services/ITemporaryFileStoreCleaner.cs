namespace BlazorBase.Files.Server.Services;

/// <summary>
/// Removes abandoned temporary uploads from the store that backs <see cref="IFileStorage"/>.
/// <see cref="TemporaryFileCleanupService"/> calls it periodically; the implementation must match
/// the storage provider (file system by default, replaced by opt-in providers such as Azure Blob Storage).
/// </summary>
public interface ITemporaryFileStoreCleaner
{
    /// <summary>
    /// Deletes temporary files that were last written more than <paramref name="maxAge"/> ago.
    /// </summary>
    /// <param name="maxAge">Minimum age of a temporary file before it is removed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of temporary files that were deleted.</returns>
    Task<int> DeleteOlderThanAsync(TimeSpan maxAge, CancellationToken cancellationToken);
}
