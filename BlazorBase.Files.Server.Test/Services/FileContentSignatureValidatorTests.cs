using BlazorBase.Files.Server.Services;
using Xunit;

namespace BlazorBase.Files.Server.Test.Services;

public sealed class FileContentSignatureValidatorTests
{
    private static readonly byte[] PngHeaderBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegHeaderBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];

    [Theory]
    [InlineData("text/plain")]
    [InlineData("image/svg+xml")]
    [InlineData("application/json")]
    public void CanValidate_ReturnsFalse_ForUnsniffableContentTypes(string contentType)
    {
        var result = FileContentSignatureValidator.CanValidate(contentType);

        Assert.False(result);
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/gif")]
    [InlineData("image/webp")]
    [InlineData("image/bmp")]
    [InlineData("application/pdf")]
    public void CanValidate_ReturnsTrue_ForSniffableContentTypes(string contentType)
    {
        var result = FileContentSignatureValidator.CanValidate(contentType);

        Assert.True(result);
    }

    [Fact]
    public void IsContentConsistentWithSignature_ReturnsFalse_WhenJpegBytesDeclaredAsPng()
    {
        var result = FileContentSignatureValidator.IsContentConsistentWithSignature(JpegHeaderBytes, "image/png");

        Assert.False(result);
    }

    [Fact]
    public void IsContentConsistentWithSignature_ReturnsTrue_WhenPngBytesDeclaredAsPng()
    {
        var result = FileContentSignatureValidator.IsContentConsistentWithSignature(PngHeaderBytes, "image/png");

        Assert.True(result);
    }

    [Fact]
    public void IsContentConsistentWithSignature_ReturnsTrue_WhenJpegBytesDeclaredAsJpeg()
    {
        var result = FileContentSignatureValidator.IsContentConsistentWithSignature(JpegHeaderBytes, "image/jpeg");

        Assert.True(result);
    }

    [Fact]
    public void IsContentConsistentWithSignature_ReturnsFalse_WhenPngBytesDeclaredAsJpeg()
    {
        var result = FileContentSignatureValidator.IsContentConsistentWithSignature(PngHeaderBytes, "image/jpeg");

        Assert.False(result);
    }
}
