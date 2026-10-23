using Azure.Storage.Blobs;
using BlazorBase.Files.Server.AzureBlob.Test.Infrastructure;
using BlazorBase.Files.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BlazorBase.Files.Server.AzureBlob.Test.Contract;

[Trait("Category", "Integration")]
public sealed class BlobFileStorageContractTests : FileStorageContractTests
{
    private BlobContainerClient? Container;

    protected override async Task<IFileStorage> CreateStorageAsync()
    {
        AzuriteEnvironment.SkipIfUnavailable();

        var containerName = $"files-contract-{Guid.NewGuid():N}";
        var provider = new AzureBlobContainerProvider(new BlazorBaseAzureBlobFileStorageSettings
        {
            ConnectionString = AzuriteEnvironment.ConnectionString,
            ContainerName = containerName,
            CreateContainerIfMissing = true,
        });

        Container = await provider.GetContainerAsync(CancellationToken.None);

        return new BlobFileStorage(provider, NullLogger<BlobFileStorage>.Instance);
    }

    protected override async Task CleanupAsync()
    {
        if (Container is not null)
            await Container.DeleteIfExistsAsync();
    }
}
