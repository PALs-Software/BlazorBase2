namespace BlazorBase.Files.Server.Services;

/// <summary>
/// Represents a readable file stream obtained from storage. The caller is responsible for
/// disposing the <see cref="Content"/> stream after use.
/// </summary>
/// <param name="Content">Readable, range-capable stream of the file content.</param>
/// <param name="ContentType">MIME content type of the file.</param>
/// <param name="FileName">Display name for the file (without extension).</param>
/// <param name="Extension">File extension including the leading dot.</param>
public sealed record FileReadResult(Stream Content, string ContentType, string FileName, string Extension) : IDisposable
{
    /// <inheritdoc/>
    public void Dispose() => Content.Dispose();
}
