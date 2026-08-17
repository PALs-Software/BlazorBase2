using BlazorBase.Files.Models;
using BlazorBase.Files.Server.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace BlazorBase.Files.Server.Test.Services;

public sealed class ImageSharpImageServiceTests
{
    private static byte[] CreateTestPngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var memoryStream = new MemoryStream();
        image.SaveAsPng(memoryStream);

        return memoryStream.ToArray();
    }

    private static byte[] CreateAnimatedGifBytes(int width, int height, int frameCount)
    {
        using var image = new Image<Rgba32>(width, height);

        for (var frameIndex = 1; frameIndex < frameCount; frameIndex++)
            image.Frames.AddFrame(new Image<Rgba32>(width, height).Frames.RootFrame);

        using var memoryStream = new MemoryStream();
        image.SaveAsGif(memoryStream);

        return memoryStream.ToArray();
    }

    [Fact]
    public async Task CreateThumbnailAsync_ThrowsImageTooLargeException_WhenPixelCountExceedsLimit()
    {
        var options = new BlazorBaseFileOptions { MaxImageDecodePixels = 1 };
        var imageService = new ImageSharpImageService(options);
        using var sourceStream = new MemoryStream(CreateTestPngBytes(4, 4));
        var destinationPath = Path.GetTempFileName();

        try
        {
            await Assert.ThrowsAsync<ImageTooLargeException>(() =>
                imageService.CreateThumbnailAsync(sourceStream, 100, destinationPath, CancellationToken.None));
        }
        finally
        {
            File.Delete(destinationPath);
        }
    }

    [Fact]
    public async Task CreateThumbnailAsync_Succeeds_WhenPixelCountWithinLimit()
    {
        var options = new BlazorBaseFileOptions();
        var imageService = new ImageSharpImageService(options);
        using var sourceStream = new MemoryStream(CreateTestPngBytes(4, 4));
        var destinationPath = Path.GetTempFileName();

        try
        {
            await imageService.CreateThumbnailAsync(sourceStream, 100, destinationPath, CancellationToken.None);

            Assert.True(new FileInfo(destinationPath).Length > 0);
        }
        finally
        {
            File.Delete(destinationPath);
        }
    }

    [Fact]
    public async Task ResizeToMaxSizeAsync_ThrowsImageTooLargeException_WhenPixelCountExceedsLimit()
    {
        var options = new BlazorBaseFileOptions { MaxImageDecodePixels = 1 };
        var imageService = new ImageSharpImageService(options);
        var imageBytes = CreateTestPngBytes(4, 4);

        await Assert.ThrowsAsync<ImageTooLargeException>(() =>
            imageService.ResizeToMaxSizeAsync(imageBytes, 100, CancellationToken.None));
    }

    [Fact]
    public async Task ResizeToMaxSizeAsync_Succeeds_WhenPixelCountWithinLimit()
    {
        var options = new BlazorBaseFileOptions();
        var imageService = new ImageSharpImageService(options);
        var imageBytes = CreateTestPngBytes(4, 4);

        var result = await imageService.ResizeToMaxSizeAsync(imageBytes, 100, CancellationToken.None);

        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task CreateThumbnailAsync_Succeeds_ForAnimatedImage_DecodingOnlyTheFirstFrame()
    {
        var options = new BlazorBaseFileOptions();
        var imageService = new ImageSharpImageService(options);
        using var sourceStream = new MemoryStream(CreateAnimatedGifBytes(4, 4, frameCount: 5));
        var destinationPath = Path.GetTempFileName();

        try
        {
            await imageService.CreateThumbnailAsync(sourceStream, 100, destinationPath, CancellationToken.None);

            Assert.True(new FileInfo(destinationPath).Length > 0);
        }
        finally
        {
            File.Delete(destinationPath);
        }
    }
}
