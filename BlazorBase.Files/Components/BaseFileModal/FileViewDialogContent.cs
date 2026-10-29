using BlazorBase.Files.Models;

namespace BlazorBase.Files.Components.BaseFileModal;

/// <summary>
/// Configuration of a <see cref="BaseFileViewDialog"/>: the file, its signed download URL and the localized texts.
/// </summary>
public sealed class FileViewDialogContent
{
    public required FileUploadResult File { get; init; }

    public required string DownloadUrl { get; init; }

    public required string ImageAltText { get; init; }

    public required string DownloadText { get; init; }

    public required string CloseText { get; init; }
}
