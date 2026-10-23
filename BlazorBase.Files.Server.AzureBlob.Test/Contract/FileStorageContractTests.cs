using System.Security.Cryptography;
using BlazorBase.Files.Server.Services;
using Xunit;

namespace BlazorBase.Files.Server.AzureBlob.Test.Contract;

/// <summary>
/// Behaviour every <see cref="IFileStorage"/> provider must share. Derived classes supply the provider.
/// </summary>
public abstract class FileStorageContractTests : IAsyncLifetime
{
    protected IFileStorage Storage { get; private set; } = null!;

    protected abstract Task<IFileStorage> CreateStorageAsync();

    protected virtual Task CleanupAsync() => Task.CompletedTask;

    public async Task InitializeAsync() => Storage = await CreateStorageAsync();

    public Task DisposeAsync() => CleanupAsync();

    [SkippableFact]
    public async Task SaveTemporaryAsync_ReportsSizeAndSha256()
    {
        var content = RandomBytes(4096);
        using var stream = new MemoryStream(content);

        var stored = await Storage.SaveTemporaryAsync(stream, ".bin", CancellationToken.None);

        Assert.NotEqual(Guid.Empty, stored.FileId);
        Assert.Equal(content.Length, stored.FileSize);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(), stored.Hash);
    }

    [SkippableFact]
    public async Task SaveTemporaryAsync_SameContentTwice_ReturnsDistinctIdsAndEqualHash()
    {
        var content = RandomBytes(128);

        var first = await Storage.SaveTemporaryAsync(new MemoryStream(content), ".bin", CancellationToken.None);
        var second = await Storage.SaveTemporaryAsync(new MemoryStream(content), ".bin", CancellationToken.None);

        Assert.NotEqual(first.FileId, second.FileId);
        Assert.Equal(first.Hash, second.Hash);
    }

    [SkippableFact]
    public async Task SaveTemporaryAsync_EmptyContent_ReportsZeroSize()
    {
        var stored = await Storage.SaveTemporaryAsync(new MemoryStream(), ".bin", CancellationToken.None);

        Assert.Equal(0, stored.FileSize);
        Assert.Equal(Convert.ToHexString(SHA256.HashData([])).ToLowerInvariant(), stored.Hash);
    }

    [SkippableFact]
    public async Task CommitAsync_MakesFileReadableUnderPermanentId()
    {
        var content = RandomBytes(2048);
        var stored = await Storage.SaveTemporaryAsync(new MemoryStream(content), ".png", CancellationToken.None);
        var permanentId = Guid.NewGuid();

        await Storage.CommitAsync(permanentId, stored.FileId, ".png", null, CancellationToken.None);

        using var result = await Storage.OpenReadAsync(permanentId, thumbnail: false, ".png", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(content, await ReadAllAsync(result.Content));
        Assert.Equal(permanentId.ToString(), result.FileName);
        Assert.Equal(".png", result.Extension);
        Assert.Equal("application/octet-stream", result.ContentType);
    }

    [SkippableFact]
    public async Task CommitAsync_WithThumbnail_StoresThumbnailSideFile()
    {
        var content = RandomBytes(2048);
        var thumbnail = RandomBytes(256);
        var stored = await Storage.SaveTemporaryAsync(new MemoryStream(content), ".jpg", CancellationToken.None);
        var permanentId = Guid.NewGuid();

        await Storage.CommitAsync(permanentId, stored.FileId, ".jpg", new MemoryStream(thumbnail), CancellationToken.None);

        using var thumbnailResult = await Storage.OpenReadAsync(permanentId, thumbnail: true, ".jpg", CancellationToken.None);
        using var originalResult = await Storage.OpenReadAsync(permanentId, thumbnail: false, ".jpg", CancellationToken.None);
        Assert.NotNull(thumbnailResult);
        Assert.NotNull(originalResult);
        Assert.Equal(thumbnail, await ReadAllAsync(thumbnailResult.Content));
        Assert.Equal(content, await ReadAllAsync(originalResult.Content));
    }

    [SkippableFact]
    public async Task CommitAsync_TemporaryFileIsConsumed()
    {
        var stored = await Storage.SaveTemporaryAsync(new MemoryStream(RandomBytes(64)), ".bin", CancellationToken.None);
        await Storage.CommitAsync(Guid.NewGuid(), stored.FileId, ".bin", null, CancellationToken.None);

        await Assert.ThrowsAnyAsync<IOException>(() =>
            Storage.CommitAsync(Guid.NewGuid(), stored.FileId, ".bin", null, CancellationToken.None));
    }

    [SkippableFact]
    public async Task CommitAsync_UnknownTemporaryFile_Throws()
    {
        await Assert.ThrowsAnyAsync<IOException>(() =>
            Storage.CommitAsync(Guid.NewGuid(), Guid.NewGuid(), ".bin", null, CancellationToken.None));
    }

    [SkippableFact]
    public async Task OpenReadAsync_UnknownFile_ReturnsNull()
    {
        Assert.Null(await Storage.OpenReadAsync(Guid.NewGuid(), thumbnail: false, ".bin", CancellationToken.None));
        Assert.Null(await Storage.OpenReadAsync(Guid.NewGuid(), thumbnail: true, ".bin", CancellationToken.None));
    }

    [SkippableFact]
    public async Task OpenReadAsync_ThumbnailMissing_FallsBackToOriginal()
    {
        var content = RandomBytes(512);
        var stored = await Storage.SaveTemporaryAsync(new MemoryStream(content), ".pdf", CancellationToken.None);
        var permanentId = Guid.NewGuid();
        await Storage.CommitAsync(permanentId, stored.FileId, ".pdf", null, CancellationToken.None);

        using var result = await Storage.OpenReadAsync(permanentId, thumbnail: true, ".pdf", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(content, await ReadAllAsync(result.Content));
    }

    [SkippableFact]
    public async Task OpenReadAsync_ReturnsSeekableStreamSupportingRangeReads()
    {
        var content = RandomBytes(6 * 1024 * 1024);
        var stored = await Storage.SaveTemporaryAsync(new MemoryStream(content), ".bin", CancellationToken.None);
        var permanentId = Guid.NewGuid();
        await Storage.CommitAsync(permanentId, stored.FileId, ".bin", null, CancellationToken.None);

        using var result = await Storage.OpenReadAsync(permanentId, thumbnail: false, ".bin", CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Content.CanSeek);
        Assert.Equal(content.Length, result.Content.Length);

        const int offset = 5_000_000;
        var buffer = new byte[1000];
        result.Content.Seek(offset, SeekOrigin.Begin);
        await result.Content.ReadExactlyAsync(buffer);
        Assert.Equal(content.AsSpan(offset, buffer.Length).ToArray(), buffer);

        result.Content.Seek(10, SeekOrigin.Begin);
        await result.Content.ReadExactlyAsync(buffer);
        Assert.Equal(content.AsSpan(10, buffer.Length).ToArray(), buffer);
    }

    [SkippableFact]
    public async Task DeleteAsync_RemovesFileAndThumbnail()
    {
        var stored = await Storage.SaveTemporaryAsync(new MemoryStream(RandomBytes(64)), ".jpg", CancellationToken.None);
        var permanentId = Guid.NewGuid();
        await Storage.CommitAsync(permanentId, stored.FileId, ".jpg", new MemoryStream(RandomBytes(16)), CancellationToken.None);

        await Storage.DeleteAsync(permanentId, ".jpg", CancellationToken.None);

        Assert.Null(await Storage.OpenReadAsync(permanentId, thumbnail: false, ".jpg", CancellationToken.None));
        Assert.Null(await Storage.OpenReadAsync(permanentId, thumbnail: true, ".jpg", CancellationToken.None));
    }

    [SkippableFact]
    public async Task DeleteAsync_UnknownFile_DoesNotThrow()
    {
        await Storage.DeleteAsync(Guid.NewGuid(), ".bin", CancellationToken.None);
    }

    [SkippableFact]
    public void ComputeSha256_KnownVector_ReturnsLowerCaseHex()
    {
        using var stream = new MemoryStream("abc"u8.ToArray());

        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", Storage.ComputeSha256(stream));
    }

    [SkippableFact]
    public void ComputeSha256_StreamNotAtStart_HashesWholeStream()
    {
        using var stream = new MemoryStream("abc"u8.ToArray());
        stream.Seek(2, SeekOrigin.Begin);

        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", Storage.ComputeSha256(stream));
    }

    protected static byte[] RandomBytes(int length)
    {
        var bytes = new byte[length];
        Random.Shared.NextBytes(bytes);
        return bytes;
    }

    protected static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }
}
