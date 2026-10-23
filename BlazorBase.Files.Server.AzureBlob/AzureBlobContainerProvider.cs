using Azure.Identity;
using Azure.Storage.Blobs;

namespace BlazorBase.Files.Server.AzureBlob;

/// <summary>
/// Provides the configured <see cref="BlobContainerClient"/> to the Azure Blob file provider and creates the
/// container once on first use when <see cref="BlazorBaseAzureBlobFileStorageSettings.CreateContainerIfMissing"/> is set.
/// </summary>
public sealed class AzureBlobContainerProvider
{
    private readonly BlobContainerClient Container;
    private readonly bool CreateContainerIfMissing;
    private readonly SemaphoreSlim InitializationLock = new(1, 1);
    private bool Initialized;

    /// <summary>
    /// The normalized prefix (ending with <c>/</c>) under which temporary uploads are stored.
    /// </summary>
    public string TemporaryPrefix { get; }

    /// <summary>
    /// Creates the provider from <paramref name="settings"/>. No network access happens here.
    /// </summary>
    /// <param name="settings">The storage settings; validated on creation.</param>
    public AzureBlobContainerProvider(BlazorBaseAzureBlobFileStorageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        var serviceClient = settings.ServiceUri is not null
            ? new BlobServiceClient(settings.ServiceUri, settings.TokenCredential ?? new DefaultAzureCredential())
            : new BlobServiceClient(settings.ConnectionString);

        Container = serviceClient.GetBlobContainerClient(settings.ContainerName);
        CreateContainerIfMissing = settings.CreateContainerIfMissing;
        TemporaryPrefix = settings.GetNormalizedTemporaryPrefix();
    }

    /// <summary>
    /// Returns the container client, creating the container first when configured and not yet done.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<BlobContainerClient> GetContainerAsync(CancellationToken cancellationToken)
    {
        if (Initialized || !CreateContainerIfMissing)
            return Container;

        await InitializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!Initialized)
            {
                await Container.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                Initialized = true;
            }
        }
        finally
        {
            InitializationLock.Release();
        }

        return Container;
    }
}
