using BlazorBase.Files.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorBase.Files.Server.Services;

/// <summary>
/// Background service that periodically sweeps the temporary upload directory and deletes files
/// older than <see cref="IBlazorBaseFileOptions.DeleteTemporaryFilesOlderThanSeconds"/>. Runs
/// only when <see cref="IBlazorBaseFileOptions.AutomaticallyDeleteOldTemporaryFiles"/> is
/// <see langword="true"/>. The deletion itself is delegated to the registered
/// <see cref="ITemporaryFileStoreCleaner"/>, which matches the active storage provider; the default
/// <see cref="FileSystemTemporaryFileStoreCleaner"/> refuses to operate when the configured temp path
/// is null, empty, or a filesystem root to prevent accidental wide-scope deletion.
/// </summary>
public class TemporaryFileCleanupService(
    IBlazorBaseFileOptions options,
    ITemporaryFileStoreCleaner cleaner,
    ILogger<TemporaryFileCleanupService> logger) : BackgroundService
{
    #region Injects
    private readonly IBlazorBaseFileOptions Options = options;
    private readonly ITemporaryFileStoreCleaner Cleaner = cleaner;
    private readonly ILogger<TemporaryFileCleanupService> Logger = logger;
    #endregion

    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Creates the service with the file system cleaner, as before the cleaner was introduced.
    /// </summary>
    public TemporaryFileCleanupService(IBlazorBaseFileOptions options, ILogger<TemporaryFileCleanupService> logger)
        : this(options, new FileSystemTemporaryFileStoreCleaner(options, logger), logger)
    {
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Options.AutomaticallyDeleteOldTemporaryFiles)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(SweepInterval, stoppingToken).ConfigureAwait(false);
            await SweepAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        var maxAge = TimeSpan.FromSeconds(Options.DeleteTemporaryFilesOlderThanSeconds);
        var deleted = await Cleaner.DeleteOlderThanAsync(maxAge, cancellationToken).ConfigureAwait(false);

        if (deleted > 0)
            Logger.LogDebug("Temporary file cleanup deleted {Count} file(s).", deleted);
    }
}
