using BlazorBase.Files.Models;
using BlazorBase.Files.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BlazorBase.Files.Server.Test.Services;

public sealed class FileSystemFileStorageTests : IDisposable
{
    private readonly string TempRoot = Path.Combine(Path.GetTempPath(), $"blazorbase-files-test-{Guid.NewGuid():N}");
    private readonly FileSystemFileStorage Storage;

    public FileSystemFileStorageTests()
    {
        var options = new BlazorBaseFileOptions
        {
            FileStorePath = Path.Combine(TempRoot, "Store"),
            TempFileStorePath = Path.Combine(TempRoot, "Temp"),
        };
        Storage = new FileSystemFileStorage(options, NullLogger<FileSystemFileStorage>.Instance);
    }

    // Regression test: SaveTemporaryAsync used to read the size back via a fresh
    // FileInfo(filePath).Length immediately after CopyToAsync, while the FileStream's own
    // 81920-byte write buffer could still be holding the (smaller) content unflushed to disk —
    // a race that under-reported FileSize (often as 0) for any file below the buffer size.
    [Fact]
    public async Task SaveTemporaryAsync_FileSmallerThanWriteBuffer_ReportsActualSize()
    {
        var content = new byte[4096];
        Random.Shared.NextBytes(content);
        using var contentStream = new MemoryStream(content);

        var storedFile = await Storage.SaveTemporaryAsync(contentStream, ".bin", CancellationToken.None);

        Assert.Equal(content.Length, storedFile.FileSize);
        Assert.Equal(content.Length, new FileInfo(storedFile.FilePath).Length);
    }

    public void Dispose()
    {
        if (Directory.Exists(TempRoot))
            Directory.Delete(TempRoot, recursive: true);
    }
}
