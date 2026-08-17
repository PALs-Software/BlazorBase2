using BlazorBase.Files.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorBase.Files.Server.Services;

/// <summary>
/// Background service that periodically sweeps the temporary upload directory and deletes files
/// older than <see cref="IBlazorBaseFileOptions.DeleteTemporaryFilesOlderThanSeconds"/>. Runs
/// only when <see cref="IBlazorBaseFileOptions.AutomaticallyDeleteOldTemporaryFiles"/> is
/// <see langword="true"/>. Refuses to operate when the configured temp path is null, empty, or
/// a filesystem root to prevent accidental wide-scope deletion.
/// </summary>
public class TemporaryFileCleanupService(
    IBlazorBaseFileOptions options,
    ILogger<TemporaryFileCleanupService> logger) : BackgroundService
{
    #region Injects
    private readonly IBlazorBaseFileOptions Options = options;
    private readonly ILogger<TemporaryFileCleanupService> Logger = logger;
    #endregion

    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(30);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Options.AutomaticallyDeleteOldTemporaryFiles)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(SweepInterval, stoppingToken).ConfigureAwait(false);
            Sweep();
        }
    }

    private void Sweep()
    {
        var tempPath = Options.TempFileStorePath;

        if (string.IsNullOrWhiteSpace(tempPath))
        {
            Logger.LogWarning("Temporary file cleanup skipped: TempFileStorePath is not configured.");
            return;
        }

        var fullPath = Path.GetFullPath(tempPath);
        var root = Path.GetPathRoot(fullPath);

        if (string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                root?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning("Temporary file cleanup refused: TempFileStorePath resolves to a filesystem root ({FullPath}). Configure a dedicated subdirectory.", fullPath);
            return;
        }

        if (!Directory.Exists(fullPath))
            return;

        var threshold = DateTime.UtcNow.AddSeconds(-(double)Options.DeleteTemporaryFilesOlderThanSeconds);

        foreach (var filePath in Directory.EnumerateFiles(fullPath))
        {
            try
            {
                var lastWrite = File.GetLastWriteTimeUtc(filePath);
                if (lastWrite < threshold)
                    File.Delete(filePath);
            }
            catch (Exception exception)
            {
                Logger.LogWarning(exception, "Failed to delete temporary file {FilePath}.", filePath);
            }
        }
    }
}
