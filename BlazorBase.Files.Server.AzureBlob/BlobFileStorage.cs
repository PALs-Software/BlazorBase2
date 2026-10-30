using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BlazorBase.Files.Server.Services;
using Microsoft.Extensions.Logging;

namespace BlazorBase.Files.Server.AzureBlob;

/// <summary>
/// Azure Blob Storage implementation of <see cref="IFileStorage"/>. Object names are identical to the file system
/// provider (<c>{id:N}{ext}</c> and <c>{id:N}_thumb{ext}</c> at the container root) so a file store can be copied 1:1
/// with AzCopy. Temporary uploads live under the configured temporary prefix (default <c>temp/</c>); committing is a
/// server-side copy followed by a delete of the temporary blob. Names are derived only from <see cref="Guid"/> values
/// and stored extensions, never from client-supplied file names.
/// </summary>
public class BlobFileStorage(AzureBlobContainerProvider containerProvider, ILogger<BlobFileStorage> logger) : IFileStorage
{
    #region Injects
    private readonly AzureBlobContainerProvider ContainerProvider = containerProvider;
    private readonly ILogger<BlobFileStorage> Logger = logger;
    #endregion

    private const string ThumbnailSuffix = "_thumb";
    private const string DefaultContentType = "application/octet-stream";

    /// <inheritdoc/>
    public async Task<StoredFile> SaveTemporaryAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ValidateExtension(extension);

        var container = await ContainerProvider.GetContainerAsync(cancellationToken).ConfigureAwait(false);

        var tempFileId = Guid.NewGuid();
        var blobName = BuildTemporaryName(tempFileId, extension);
        var fileSize = content.Length - content.Position;

        await container.GetBlobClient(blobName).UploadAsync(content, overwrite: true, cancellationToken).ConfigureAwait(false);

        var hash = ComputeSha256(content);

        return new StoredFile(tempFileId, blobName, hash, fileSize);
    }

    /// <inheritdoc/>
    public async Task CommitAsync(Guid permanentFileId, Guid tempFileId, string extension, Stream? thumbnailStream, CancellationToken cancellationToken)
    {
        ValidateExtension(extension);

        var container = await ContainerProvider.GetContainerAsync(cancellationToken).ConfigureAwait(false);

        var source = container.GetBlobClient(BuildTemporaryName(tempFileId, extension));
        var destination = container.GetBlobClient(BuildName(permanentFileId, extension));

        try
        {
            var copyOperation = await destination.StartCopyFromUriAsync(source.Uri, cancellationToken: cancellationToken).ConfigureAwait(false);
            await copyOperation.WaitForCompletionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            throw new FileNotFoundException($"The temporary file '{source.Name}' does not exist.", source.Name, exception);
        }

        await source.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        if (thumbnailStream is null)
            return;

        await container.GetBlobClient(BuildThumbnailName(permanentFileId, extension))
            .UploadAsync(thumbnailStream, overwrite: true, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<FileReadResult?> OpenReadAsync(Guid fileId, bool thumbnail, string extension, CancellationToken cancellationToken)
    {
        ValidateExtension(extension);

        var container = await ContainerProvider.GetContainerAsync(cancellationToken).ConfigureAwait(false);

        var name = thumbnail ? BuildThumbnailName(fileId, extension) : BuildName(fileId, extension);
        var stream = await TryOpenReadAsync(container.GetBlobClient(name), cancellationToken).ConfigureAwait(false);

        if (stream is null && thumbnail)
            stream = await TryOpenReadAsync(container.GetBlobClient(BuildName(fileId, extension)), cancellationToken).ConfigureAwait(false);

        if (stream is null)
            return null;

        return new FileReadResult(stream, DefaultContentType, fileId.ToString(), extension);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid fileId, string extension, CancellationToken cancellationToken)
    {
        ValidateExtension(extension);

        var container = await ContainerProvider.GetContainerAsync(cancellationToken).ConfigureAwait(false);

        await TryDeleteAsync(container, BuildName(fileId, extension), cancellationToken).ConfigureAwait(false);
        await TryDeleteAsync(container, BuildThumbnailName(fileId, extension), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public string ComputeSha256(Stream content) => FileContentHasher.ComputeSha256(content);

    private static async Task<Stream?> TryOpenReadAsync(BlobClient blob, CancellationToken cancellationToken)
    {
        try
        {
            return await blob.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false), cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    private async Task TryDeleteAsync(BlobContainerClient container, string blobName, CancellationToken cancellationToken)
    {
        try
        {
            await container.GetBlobClient(blobName).DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException exception)
        {
            Logger.LogWarning(exception, "Failed to delete blob {BlobName}.", blobName);
        }
    }

    private static void ValidateExtension(string extension)
    {
        if (extension.AsSpan().ContainsAny('/', '\\'))
            throw new ArgumentException("The file extension must not contain path separators.", nameof(extension));
    }

    private static string BuildName(Guid fileId, string extension) =>
        $"{fileId:N}{extension}";

    private static string BuildThumbnailName(Guid fileId, string extension) =>
        $"{fileId:N}{ThumbnailSuffix}{extension}";

    private string BuildTemporaryName(Guid fileId, string extension) =>
        $"{ContainerProvider.TemporaryPrefix}{BuildName(fileId, extension)}";
}
