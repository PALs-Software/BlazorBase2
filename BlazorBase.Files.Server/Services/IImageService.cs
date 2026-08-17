namespace BlazorBase.Files.Server.Services;

/// <summary>
/// Abstracts managed image processing (resize, thumbnail). Implementations must not attempt
/// to decode or process non-image file streams.
/// </summary>
public interface IImageService
{
    /// <summary>
    /// Resizes <paramref name="input"/> so that its longest edge is at most
    /// <paramref name="maxEdge"/> pixels (aspect ratio preserved) and writes the result to
    /// <paramref name="destinationPath"/>.
    /// </summary>
    /// <param name="input">Source image stream. The stream is read from its current position.</param>
    /// <param name="maxEdge">Maximum pixel length of the longer image dimension.</param>
    /// <param name="destinationPath">Absolute path of the output file to create or overwrite.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateThumbnailAsync(Stream input, int maxEdge, string destinationPath, CancellationToken cancellationToken);

    /// <summary>
    /// Resizes <paramref name="input"/> in-memory so that its longest edge is at most
    /// <paramref name="maxEdge"/> pixels (aspect ratio preserved) and returns the result as a
    /// JPEG byte array.
    /// </summary>
    /// <param name="input">Source image bytes.</param>
    /// <param name="maxEdge">Maximum pixel length of the longer image dimension.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<byte[]> ResizeToMaxSizeAsync(byte[] input, int maxEdge, CancellationToken cancellationToken);
}
