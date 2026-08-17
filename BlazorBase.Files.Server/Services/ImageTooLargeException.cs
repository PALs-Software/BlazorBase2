namespace BlazorBase.Files.Server.Services;

/// <summary>
/// Thrown when an image's decoded pixel dimensions (width × height) would exceed the configured
/// <see cref="BlazorBase.Files.Models.IBlazorBaseFileOptions.MaxImageDecodePixels"/> limit. Signals
/// a controlled rejection of a potential decompression-bomb image rather than an unbounded-memory
/// decode.
/// </summary>
public sealed class ImageTooLargeException(string message) : Exception(message);
