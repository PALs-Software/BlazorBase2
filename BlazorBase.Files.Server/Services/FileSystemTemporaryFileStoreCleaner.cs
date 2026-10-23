using BlazorBase.Files.Models;
using Microsoft.Extensions.Logging;

namespace BlazorBase.Files.Server.Services;

/// <summary>
/// File system implementation of <see cref="ITemporaryFileStoreCleaner"/>. Sweeps
/// <see cref="IBlazorBaseFileOptions.TempFileStorePath"/> and refuses to operate when the configured
/// path is null, empty, or a filesystem root to prevent accidental wide-scope deletion.
/// </summary>
public class FileSystemTemporaryFileStoreCleaner(IBlazorBaseFileOptions options, ILogger logger) : ITemporaryFileStoreCleaner
{
    #region Injects
    private readonly IBlazorBaseFileOptions Options = options;
    private readonly ILogger Logger = logger;
    #endregion

    /// <inheritdoc/>
    public Task<int> DeleteOlderThanAsync(TimeSpan maxAge, CancellationToken cancellationToken)
    {
        var tempPath = Options.TempFileStorePath;

        if (string.IsNullOrWhiteSpace(tempPath))
        {
            Logger.LogWarning("Temporary file cleanup skipped: TempFileStorePath is not configured.");
            return Task.FromResult(0);
        }

        var fullPath = Path.GetFullPath(tempPath);
        var root = Path.GetPathRoot(fullPath);

        if (string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                root?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning("Temporary file cleanup refused: TempFileStorePath resolves to a filesystem root ({FullPath}). Configure a dedicated subdirectory.", fullPath);
            return Task.FromResult(0);
        }

        if (!Directory.Exists(fullPath))
            return Task.FromResult(0);

        var threshold = DateTime.UtcNow - maxAge;
        var deleted = 0;

        foreach (var filePath in Directory.EnumerateFiles(fullPath))
        {
            try
            {
                var lastWrite = File.GetLastWriteTimeUtc(filePath);
                if (lastWrite < threshold)
                {
                    File.Delete(filePath);
                    deleted++;
                }
            }
            catch (Exception exception)
            {
                Logger.LogWarning(exception, "Failed to delete temporary file {FilePath}.", filePath);
            }
        }

        return Task.FromResult(deleted);
    }
}
