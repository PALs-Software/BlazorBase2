using BlazorBase.Files.Models;
using BlazorBase.Files.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BlazorBase.Files.Server.Test.Services;

public sealed class FileSystemTemporaryFileStoreCleanerTests : IDisposable
{
    private readonly string TempRoot = Path.Combine(Path.GetTempPath(), $"blazorbase-cleaner-test-{Guid.NewGuid():N}");
    private readonly string TempPath;

    public FileSystemTemporaryFileStoreCleanerTests()
    {
        TempPath = Path.Combine(TempRoot, "Temp");
        Directory.CreateDirectory(TempPath);
    }

    private FileSystemTemporaryFileStoreCleaner CreateCleaner(string? tempFileStorePath) =>
        new(new BlazorBaseFileOptions { TempFileStorePath = tempFileStorePath! }, NullLogger.Instance);

    [Fact]
    public async Task DeleteOlderThanAsync_DeletesOnlyFilesOlderThanMaxAge()
    {
        var oldFile = Path.Combine(TempPath, "old.bin");
        var newFile = Path.Combine(TempPath, "new.bin");
        File.WriteAllBytes(oldFile, [1]);
        File.WriteAllBytes(newFile, [1]);
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddHours(-2));

        var deleted = await CreateCleaner(TempPath).DeleteOlderThanAsync(TimeSpan.FromHours(1), CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.False(File.Exists(oldFile));
        Assert.True(File.Exists(newFile));
    }

    [Fact]
    public async Task DeleteOlderThanAsync_MissingDirectory_DeletesNothing()
    {
        var deleted = await CreateCleaner(Path.Combine(TempRoot, "missing")).DeleteOlderThanAsync(TimeSpan.Zero, CancellationToken.None);

        Assert.Equal(0, deleted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task DeleteOlderThanAsync_PathNotConfigured_DeletesNothing(string? path)
    {
        var deleted = await CreateCleaner(path).DeleteOlderThanAsync(TimeSpan.Zero, CancellationToken.None);

        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_PathIsFilesystemRoot_RefusesToDelete()
    {
        var root = Path.GetPathRoot(Path.GetFullPath(TempRoot))!;

        var deleted = await CreateCleaner(root).DeleteOlderThanAsync(TimeSpan.FromDays(36500), CancellationToken.None);

        Assert.Equal(0, deleted);
    }

    public void Dispose()
    {
        if (Directory.Exists(TempRoot))
            Directory.Delete(TempRoot, recursive: true);
    }
}
