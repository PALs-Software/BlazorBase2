using BlazorBase.Files.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace BlazorBase.Files.Server.Services;

/// <summary>
/// Managed cross-platform image service built on SixLabors.ImageSharp. Never decodes
/// non-image streams — callers must guard with <see cref="BlazorBase.Files.Models.IBaseFile.IsImage()"/>
/// before invoking.
/// </summary>
public class ImageSharpImageService(IBlazorBaseFileOptions options) : IImageService
{
    #region Injects
    private readonly IBlazorBaseFileOptions Options = options;
    #endregion

    private static readonly DecoderOptions SingleFrameDecoderOptions = new() { MaxFrames = 1 };

    /// <inheritdoc/>
    public async Task CreateThumbnailAsync(Stream input, int maxEdge, string destinationPath, CancellationToken cancellationToken)
    {
        await GuardAgainstOversizedImageAsync(input, cancellationToken).ConfigureAwait(false);

        using var image = await Image.LoadAsync(SingleFrameDecoderOptions, input, cancellationToken).ConfigureAwait(false);

        var size = ComputeTargetSize(image.Width, image.Height, maxEdge);
        image.Mutate(context => context.Resize(size.Width, size.Height));

        await image.SaveAsJpegAsync(destinationPath, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<byte[]> ResizeToMaxSizeAsync(byte[] input, int maxEdge, CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream(input);

        await GuardAgainstOversizedImageAsync(memoryStream, cancellationToken).ConfigureAwait(false);

        using var image = await Image.LoadAsync(SingleFrameDecoderOptions, memoryStream, cancellationToken).ConfigureAwait(false);

        var size = ComputeTargetSize(image.Width, image.Height, maxEdge);
        image.Mutate(context => context.Resize(size.Width, size.Height));

        using var output = new MemoryStream();
        await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = 85 }, cancellationToken).ConfigureAwait(false);
        return output.ToArray();
    }

    private async Task GuardAgainstOversizedImageAsync(Stream input, CancellationToken cancellationToken)
    {
        var originalPosition = input.Position;

        var imageInfo = await Image.IdentifyAsync(SingleFrameDecoderOptions, input, cancellationToken).ConfigureAwait(false);

        if (Options.MaxImageDecodePixels > 0 && (long)imageInfo.Width * imageInfo.Height > Options.MaxImageDecodePixels)
            throw new ImageTooLargeException($"Image dimensions {imageInfo.Width}x{imageInfo.Height} exceed the maximum permitted pixel count of {Options.MaxImageDecodePixels}.");

        input.Seek(originalPosition, SeekOrigin.Begin);
    }

    private static (int Width, int Height) ComputeTargetSize(int width, int height, int maxEdge)
    {
        if (width <= maxEdge && height <= maxEdge)
            return (width, height);

        if (width >= height)
        {
            var scaledHeight = (int)Math.Round(height * (double)maxEdge / width);
            return (maxEdge, scaledHeight);
        }

        var scaledWidth = (int)Math.Round(width * (double)maxEdge / height);
        return (scaledWidth, maxEdge);
    }
}
