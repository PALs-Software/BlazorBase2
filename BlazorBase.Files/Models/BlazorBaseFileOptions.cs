namespace BlazorBase.Files.Models;

/// <summary>
/// Default, mutable implementation of <see cref="IBlazorBaseFileOptions"/>. Register and configure
/// via <c>AddBlazorBaseFiles(options => { … })</c>; do not use a static singleton.
/// </summary>
public class BlazorBaseFileOptions : IBlazorBaseFileOptions
{
    public string ControllerRoute { get; set; } = "api/files";
    public string FileStorePath { get; set; } = "FileStore";
    public string TempFileStorePath { get; set; } = "FileStore/Temp";
    public ulong MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024;
    public string[] AllowedContentTypes { get; set; } = [];
    public bool UseImageThumbnails { get; set; } = true;
    public int ImageThumbnailSize { get; set; } = 400;
    public bool AutomaticallyDeleteOldTemporaryFiles { get; set; } = true;
    public uint DeleteTemporaryFilesOlderThanSeconds { get; set; } = 60 * 60 * 24;
    public bool ValidateContentTypeSignature { get; set; } = true;
    public long MaxImageDecodePixels { get; set; } = 50_000_000;
}
