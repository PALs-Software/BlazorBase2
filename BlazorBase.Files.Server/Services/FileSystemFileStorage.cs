using System.Security.Cryptography;
using BlazorBase.Files.Models;
using Microsoft.Extensions.Logging;

namespace BlazorBase.Files.Server.Services;

/// <summary>
/// Filesystem-backed implementation of <see cref="IFileStorage"/>. On-disk filenames are
/// derived exclusively from validated <see cref="Guid"/> values and stored extensions;
/// client-supplied filenames are never used in path construction, preventing path traversal.
/// </summary>
public class FileSystemFileStorage(IBlazorBaseFileOptions options, ILogger<FileSystemFileStorage> logger) : IFileStorage
{
    #region Injects
    private readonly IBlazorBaseFileOptions Options = options;
    private readonly ILogger<FileSystemFileStorage> Logger = logger;
    #endregion

    private const string ThumbnailSuffix = "_thumb";

    /// <inheritdoc/>
    public async Task<StoredFile> SaveTemporaryAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Options.TempFileStorePath);

        var tempFileId = Guid.NewGuid();
        var filePath = BuildPath(Options.TempFileStorePath, tempFileId, extension);

        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
        await content.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);

        // fileStream.Length, not a fresh FileInfo(filePath).Length: the FileStream's internal
        // write buffer (81920 bytes) can still hold unflushed data at this point for any file
        // smaller than that, so re-stat'ing the path from the filesystem race-condition-reads a
        // stale (sometimes zero) size. The stream already knows its own logical length.
        var fileSize = fileStream.Length;

        content.Seek(0, SeekOrigin.Begin);
        var hash = ComputeSha256(content);

        return new StoredFile(tempFileId, filePath, hash, fileSize);
    }

    /// <inheritdoc/>
    public async Task CommitAsync(Guid permanentFileId, Guid tempFileId, string extension, Stream? thumbnailStream, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Options.FileStorePath);

        var sourcePath = BuildPath(Options.TempFileStorePath, tempFileId, extension);
        var destPath = BuildPath(Options.FileStorePath, permanentFileId, extension);

        File.Move(sourcePath, destPath, overwrite: true);

        if (thumbnailStream is null)
            return;

        var thumbPath = BuildThumbnailPath(Options.FileStorePath, permanentFileId, extension);
        using var fileStream = new FileStream(thumbPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
        await thumbnailStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<FileReadResult?> OpenReadAsync(Guid fileId, bool thumbnail, string extension, CancellationToken cancellationToken)
    {
        var filePath = thumbnail
            ? BuildThumbnailPath(Options.FileStorePath, fileId, extension)
            : BuildPath(Options.FileStorePath, fileId, extension);

        if (!File.Exists(filePath))
        {
            if (thumbnail)
            {
                var originalPath = BuildPath(Options.FileStorePath, fileId, extension);
                if (File.Exists(originalPath))
                    return Task.FromResult<FileReadResult?>(new FileReadResult(File.OpenRead(originalPath), "application/octet-stream", fileId.ToString(), extension));
            }

            return Task.FromResult<FileReadResult?>(null);
        }

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        var result = new FileReadResult(stream, "application/octet-stream", fileId.ToString(), extension);
        return Task.FromResult<FileReadResult?>(result);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(Guid fileId, string extension, CancellationToken cancellationToken)
    {
        var filePath = BuildPath(Options.FileStorePath, fileId, extension);
        var thumbPath = BuildThumbnailPath(Options.FileStorePath, fileId, extension);

        TryDeleteFile(filePath);
        TryDeleteFile(thumbPath);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public string ComputeSha256(Stream content)
    {
        content.Seek(0, SeekOrigin.Begin);
        var hashBytes = SHA256.HashData(content);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string BuildPath(string directory, Guid fileId, string extension) =>
        Path.Combine(directory, $"{fileId:N}{extension}");

    private static string BuildThumbnailPath(string directory, Guid fileId, string extension) =>
        Path.Combine(directory, $"{fileId:N}{ThumbnailSuffix}{extension}");

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Failed to delete file at path {Path}.", path);
        }
    }
}
