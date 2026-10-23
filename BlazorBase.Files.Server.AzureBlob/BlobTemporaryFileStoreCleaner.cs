using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BlazorBase.Files.Server.Services;
using Microsoft.Extensions.Logging;

namespace BlazorBase.Files.Server.AzureBlob;

/// <summary>
/// Azure Blob Storage implementation of <see cref="ITemporaryFileStoreCleaner"/>. Deletes blobs under the
/// configured temporary prefix whose last modification is older than the requested age. Failures talking to
/// the storage account are logged and do not stop the hosting process; the next sweep retries.
/// </summary>
public class BlobTemporaryFileStoreCleaner(AzureBlobContainerProvider containerProvider, ILogger<BlobTemporaryFileStoreCleaner> logger) : ITemporaryFileStoreCleaner
{
    #region Injects
    private readonly AzureBlobContainerProvider ContainerProvider = containerProvider;
    private readonly ILogger<BlobTemporaryFileStoreCleaner> Logger = logger;
    #endregion

    /// <inheritdoc/>
    public async Task<int> DeleteOlderThanAsync(TimeSpan maxAge, CancellationToken cancellationToken)
    {
        var deleted = 0;
        var threshold = DateTimeOffset.UtcNow - maxAge;

        try
        {
            var container = await ContainerProvider.GetContainerAsync(cancellationToken).ConfigureAwait(false);

            await foreach (var blob in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, ContainerProvider.TemporaryPrefix, cancellationToken).ConfigureAwait(false))
            {
                var lastModified = blob.Properties.LastModified;
                if (lastModified is null || lastModified.Value >= threshold)
                    continue;

                if (await TryDeleteAsync(container, blob.Name, cancellationToken).ConfigureAwait(false))
                    deleted++;
            }
        }
        catch (RequestFailedException exception)
        {
            Logger.LogWarning(exception, "Temporary blob cleanup failed while listing blobs under prefix {Prefix}.", ContainerProvider.TemporaryPrefix);
        }

        return deleted;
    }

    private async Task<bool> TryDeleteAsync(BlobContainerClient container, string blobName, CancellationToken cancellationToken)
    {
        try
        {
            return await container.GetBlobClient(blobName).DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException exception)
        {
            Logger.LogWarning(exception, "Failed to delete temporary blob {BlobName}.", blobName);
            return false;
        }
    }
}
