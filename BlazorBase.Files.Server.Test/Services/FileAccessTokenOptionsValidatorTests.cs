using System.Security.Cryptography;
using BlazorBase.Files.Server.Services;
using Xunit;

namespace BlazorBase.Files.Server.Test.Services;

public sealed class FileAccessTokenOptionsValidatorTests
{
    private readonly FileAccessTokenOptionsValidator Validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Fails_ForNullOrEmptySigningKey(string? signingKeyBase64)
    {
        var options = new FileAccessTokenOptions { SigningKeyBase64 = signingKeyBase64! };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_Fails_ForNonBase64SigningKey()
    {
        var options = new FileAccessTokenOptions { SigningKeyBase64 = "not-valid-base64!!!" };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_Fails_ForSigningKeyShorterThan32Bytes()
    {
        var options = new FileAccessTokenOptions { SigningKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)) };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_Succeeds_ForSigningKeyAtLeast32Bytes()
    {
        var options = new FileAccessTokenOptions { SigningKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };

        var result = Validator.Validate(null, options);

        Assert.False(result.Failed);
    }
}
