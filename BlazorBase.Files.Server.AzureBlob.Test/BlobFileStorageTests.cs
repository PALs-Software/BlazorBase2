using Azure.Storage.Blobs;
using BlazorBase.Files.Server.AzureBlob.Test.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BlazorBase.Files.Server.AzureBlob.Test;

[Trait("Category", "Integration")]
public sealed class BlobFileStorageTests : IAsyncLifetime
{
    private BlobContainerClient Container = null!;
    private AzureBlobContainerProvider Provider = null!;
    private BlobFileStorage Storage = null!;
    private BlobTemporaryFileStoreCleaner Cleaner = null!;

    public async Task InitializeAsync()
    {
        AzuriteEnvironment.SkipIfUnavailable();

        Provider = new AzureBlobContainerProvider(new BlazorBaseAzureBlobFileStorageSettings
        {
            ConnectionString = AzuriteEnvironment.ConnectionString,
            ContainerName = $"files-blob-{Guid.NewGuid():N}",
            CreateContainerIfMissing = true,
        });

        Container = await Provider.GetContainerAsync(CancellationToken.None);
        Storage = new BlobFileStorage(Provider, NullLogger<BlobFileStorage>.Instance);
        Cleaner = new BlobTemporaryFileStoreCleaner(Provider, NullLogger<BlobTemporaryFileStoreCleaner>.Instance);
    }

    public async Task DisposeAsync()
    {
        if (Container is not null)
            await Container.DeleteIfExistsAsync();
    }

    [SkippableFact]
    public async Task SaveTemporaryAsync_StoresBlobUnderTemporaryPrefixWithFileSystemNaming()
    {
        var stored = await Storage.SaveTemporaryAsync(new MemoryStream(new byte[10]), ".jpg", CancellationToken.None);

        Assert.Equal($"temp/{stored.FileId:N}.jpg", stored.FilePath);
        Assert.Equal([$"temp/{stored.FileId:N}.jpg"], await ListBlobNamesAsync());
    }

    [SkippableFact]
    public async Task CommitAsync_UsesFileSystemNamingAtContainerRootAndRemovesTemporaryBlob()
    {
        var stored = await Storage.SaveTemporaryAsync(new MemoryStream(new byte[10]), ".jpg", CancellationToken.None);
        var permanentId = Guid.NewGuid();

        await Storage.CommitAsync(permanentId, stored.FileId, ".jpg", new MemoryStream(new byte[4]), CancellationToken.None);

        Assert.Equal([$"{permanentId:N}.jpg", $"{permanentId:N}_thumb.jpg"], await ListBlobNamesAsync());
    }

    [SkippableFact]
    public async Task DeleteOlderThanAsync_DeletesOldTemporaryBlobsOnly()
    {
        var abandoned = await Storage.SaveTemporaryAsync(new MemoryStream(new byte[10]), ".bin", CancellationToken.None);
        var committed = await Storage.SaveTemporaryAsync(new MemoryStream(new byte[10]), ".bin", CancellationToken.None);
        var permanentId = Guid.NewGuid();
        await Storage.CommitAsync(permanentId, committed.FileId, ".bin", null, CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        var deleted = await Cleaner.DeleteOlderThanAsync(TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Equal([$"{permanentId:N}.bin"], await ListBlobNamesAsync());
        Assert.DoesNotContain(abandoned.FilePath, await ListBlobNamesAsync());
    }

    [SkippableFact]
    public async Task DeleteOlderThanAsync_RecentTemporaryBlobs_AreKept()
    {
        var recent = await Storage.SaveTemporaryAsync(new MemoryStream(new byte[10]), ".bin", CancellationToken.None);

        var deleted = await Cleaner.DeleteOlderThanAsync(TimeSpan.FromHours(1), CancellationToken.None);

        Assert.Equal(0, deleted);
        Assert.Equal([recent.FilePath], await ListBlobNamesAsync());
    }

    [SkippableFact]
    public async Task OpenReadAsync_ExtensionWithPathSeparator_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Storage.OpenReadAsync(Guid.NewGuid(), thumbnail: false, "/../x", CancellationToken.None));
    }

    [SkippableFact]
    public async Task GetContainerAsync_ContainerMissingAndNotCreated_DoesNotCreateContainer()
    {
        var provider = new AzureBlobContainerProvider(new BlazorBaseAzureBlobFileStorageSettings
        {
            ConnectionString = AzuriteEnvironment.ConnectionString,
            ContainerName = $"files-missing-{Guid.NewGuid():N}",
        });

        var container = await provider.GetContainerAsync(CancellationToken.None);
        var storage = new BlobFileStorage(provider, NullLogger<BlobFileStorage>.Instance);

        Assert.False(await container.ExistsAsync());
        Assert.Null(await storage.OpenReadAsync(Guid.NewGuid(), thumbnail: false, ".bin", CancellationToken.None));
    }

    private async Task<List<string>> ListBlobNamesAsync()
    {
        var names = new List<string>();
        await foreach (var blob in Container.GetBlobsAsync())
            names.Add(blob.Name);

        names.Sort(StringComparer.Ordinal);
        return names;
    }
}
