using BlazorBase.Files.Models;
using BlazorBase.Files.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlazorBase.Files.Server.AzureBlob.Test.Contract;

public sealed class FileSystemFileStorageContractTests : FileStorageContractTests
{
    private readonly string TempRoot = Path.Combine(Path.GetTempPath(), $"blazorbase-files-contract-{Guid.NewGuid():N}");

    protected override Task<IFileStorage> CreateStorageAsync()
    {
        var options = new BlazorBaseFileOptions
        {
            FileStorePath = Path.Combine(TempRoot, "Store"),
            TempFileStorePath = Path.Combine(TempRoot, "Temp"),
        };

        return Task.FromResult<IFileStorage>(new FileSystemFileStorage(options, NullLogger<FileSystemFileStorage>.Instance));
    }

    protected override Task CleanupAsync()
    {
        if (Directory.Exists(TempRoot))
            Directory.Delete(TempRoot, recursive: true);

        return Task.CompletedTask;
    }
}
