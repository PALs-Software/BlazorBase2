namespace BlazorBase.Components.Dialogs;

/// <summary>
/// The file the user picked and confirmed in a <see cref="BaseFileInputDialog"/>, already buffered
/// into memory so the caller can post it to a server endpoint without touching the browser stream.
/// </summary>
public sealed class FileInputDialogResult
{
    public required string FileName { get; init; }

    public required byte[] Content { get; init; }
}
