namespace BlazorBase.Files.Models;

/// <summary>
/// Options for the BlazorBase.Files module. Consumed by both client (upload client, component
/// guards) and server (storage paths, cleanup service, thumbnail generation).
/// </summary>
public interface IBlazorBaseFileOptions
{
    /// <summary>
    /// Base route of the file controller without a leading slash (e.g. <c>api/files</c>).
    /// Used by components to build download and thumbnail URLs.
    /// </summary>
    string ControllerRoute { get; set; }

    /// <summary>Server-side path to the permanent file store directory.</summary>
    string FileStorePath { get; set; }

    /// <summary>Server-side path to the temporary upload directory.</summary>
    string TempFileStorePath { get; set; }

    /// <summary>Maximum permitted upload size in bytes (enforced client- and server-side).</summary>
    ulong MaxFileSizeBytes { get; set; }

    /// <summary>
    /// Allowed MIME content types. An empty array means all types are permitted.
    /// Components and the server controller both validate against this list.
    /// </summary>
    string[] AllowedContentTypes { get; set; }

    /// <summary>When <see langword="true"/> the server generates a thumbnail for image uploads.</summary>
    bool UseImageThumbnails { get; set; }

    /// <summary>Maximum edge length in pixels for generated thumbnails.</summary>
    int ImageThumbnailSize { get; set; }

    /// <summary>
    /// When <see langword="true"/> the <c>TemporaryFileCleanupService</c> (server-side) runs
    /// periodically to delete orphaned temporary uploads.
    /// </summary>
    bool AutomaticallyDeleteOldTemporaryFiles { get; set; }

    /// <summary>Age threshold in seconds; temporary files older than this are eligible for deletion.</summary>
    uint DeleteTemporaryFilesOlderThanSeconds { get; set; }

    /// <summary>
    /// When <see langword="true"/> the server verifies that an uploaded file's leading bytes
    /// ("magic numbers") match a known signature for its declared content type, rejecting a
    /// mismatch with a 400 response. Content types without a reliable signature (e.g. text-based
    /// formats) are always treated as unverifiable and allowed through regardless of this setting.
    /// </summary>
    bool ValidateContentTypeSignature { get; set; }

    /// <summary>
    /// Maximum number of pixels (width × height) permitted when decoding an image for thumbnail
    /// generation or resizing. Guards against decompression-bomb images that decode to
    /// disproportionately large dimensions relative to their file size.
    /// </summary>
    long MaxImageDecodePixels { get; set; }
}
