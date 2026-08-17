using BlazorBase.Files.Server.Services;

namespace BlazorBase.Files.Server.Test.TestDoubles;

/// <summary>
/// Minimal in-memory <see cref="IFileStorage"/> test double. Performs no real disk I/O and
/// records whether <see cref="CommitAsync"/>/<see cref="DeleteAsync"/> were invoked, so tests can
/// assert a guard rejected an upload before any storage write occurred.
/// </summary>
public sealed class StubFileStorage : IFileStorage
{
    public bool SaveTemporaryAsyncCalled { get; private set; }

    public bool CommitAsyncCalled { get; private set; }

    public bool DeleteAsyncCalled { get; private set; }

    public string HashToReturn { get; set; } = "stub-hash";

    public long FileSizeToReturn { get; set; } = 42;

    public Task<StoredFile> SaveTemporaryAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        SaveTemporaryAsyncCalled = true;
        return Task.FromResult(new StoredFile(Guid.NewGuid(), "stub-temp-path", HashToReturn, FileSizeToReturn));
    }

    public Task CommitAsync(Guid permanentFileId, Guid tempFileId, string extension, Stream? thumbnailStream, CancellationToken cancellationToken)
    {
        CommitAsyncCalled = true;
        return Task.CompletedTask;
    }

    public Task<FileReadResult?> OpenReadAsync(Guid fileId, bool thumbnail, string extension, CancellationToken cancellationToken) =>
        Task.FromResult<FileReadResult?>(null);

    public Task DeleteAsync(Guid fileId, string extension, CancellationToken cancellationToken)
    {
        DeleteAsyncCalled = true;
        return Task.CompletedTask;
    }

    public string ComputeSha256(Stream content) => HashToReturn;
}
