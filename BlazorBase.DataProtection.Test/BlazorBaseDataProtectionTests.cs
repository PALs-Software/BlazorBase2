using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using Xunit;

namespace BlazorBase.DataProtection.Test;

[Collection("BlazorBaseDataProtectionStaticState")]
public class BlazorBaseDataProtectionTests
{
    [Fact]
    public void Protect_ThenUnprotect_RoundTripsToOriginalPlainText()
    {
        var provider = DataProtectionProvider.Create("BlazorBase.DataProtection.Test");
        BlazorBaseDataProtection.Initialize(provider);

        var ciphertext = BlazorBaseDataProtection.Protect("hello world");
        var plaintext = BlazorBaseDataProtection.Unprotect(ciphertext);

        Assert.Equal("hello world", plaintext);
        Assert.NotEqual("hello world", ciphertext);
    }

    [Fact]
    public void EncryptString_ThenDecryptStringToInsecureString_RoundTrips()
    {
        var provider = DataProtectionProvider.Create("BlazorBase.DataProtection.Test.Extensions");
        BlazorBaseDataProtection.Initialize(provider);

        var ciphertext = "my-secret-token".EncryptString();
        var plaintext = ciphertext.DecryptStringToInsecureString();

        Assert.Equal("my-secret-token", plaintext);
    }

    [Fact]
    public void DecryptStringToInsecureString_WithGarbageInput_ReturnsNull()
    {
        var provider = DataProtectionProvider.Create("BlazorBase.DataProtection.Test.Garbage");
        BlazorBaseDataProtection.Initialize(provider);

        var result = "not-a-real-ciphertext".DecryptStringToInsecureString();

        Assert.Null(result);
    }

    [Fact]
    public void Protect_WithPurpose_ThenUnprotect_WithSamePurpose_RoundTripsToOriginalPlainText()
    {
        var provider = DataProtectionProvider.Create("BlazorBase.DataProtection.Test.Purposed");
        BlazorBaseDataProtection.Initialize(provider);

        var ciphertext = BlazorBaseDataProtection.Protect("hello purposed world", "email");
        var plaintext = BlazorBaseDataProtection.Unprotect(ciphertext, "email");

        Assert.Equal("hello purposed world", plaintext);
        Assert.NotEqual("hello purposed world", ciphertext);
    }

    [Fact]
    public void EncryptString_WithPurpose_ThenDecryptStringToInsecureString_WithSamePurpose_RoundTrips()
    {
        var provider = DataProtectionProvider.Create("BlazorBase.DataProtection.Test.Purposed.Extensions");
        BlazorBaseDataProtection.Initialize(provider);

        var ciphertext = "my-purposed-token".EncryptString("email");
        var plaintext = ciphertext.DecryptStringToInsecureString("email");

        Assert.Equal("my-purposed-token", plaintext);
    }

    [Fact]
    public void Unprotect_WithDifferentPurposeThanUsedToProtect_ThrowsCryptographicException()
    {
        var provider = DataProtectionProvider.Create("BlazorBase.DataProtection.Test.CrossPurpose");
        BlazorBaseDataProtection.Initialize(provider);

        var ciphertext = BlazorBaseDataProtection.Protect("cross purpose secret", "purposeA");

        Assert.Throws<CryptographicException>(() => BlazorBaseDataProtection.Unprotect(ciphertext, "purposeB"));
    }

    [Fact]
    public void DecryptStringToInsecureString_WithDifferentPurposeThanUsedToEncrypt_ReturnsNull()
    {
        var provider = DataProtectionProvider.Create("BlazorBase.DataProtection.Test.CrossPurpose.Extensions");
        BlazorBaseDataProtection.Initialize(provider);

        var ciphertext = "cross-purpose-token".EncryptString("purposeA");
        var plaintext = ciphertext.DecryptStringToInsecureString("purposeB");

        Assert.Null(plaintext);
    }

    [Fact]
    public void Unprotect_WithNoPurposeCiphertext_UnderPurposedUnprotect_ThrowsCryptographicException()
    {
        var provider = DataProtectionProvider.Create("BlazorBase.DataProtection.Test.DefaultVersusPurposed");
        BlazorBaseDataProtection.Initialize(provider);

        var noPurposeCiphertext = BlazorBaseDataProtection.Protect("default path secret");

        Assert.Throws<CryptographicException>(() => BlazorBaseDataProtection.Unprotect(noPurposeCiphertext, "email"));
    }

    [Fact]
    public void Unprotect_WithPurposedCiphertext_UnderNoPurposeUnprotect_ThrowsCryptographicException()
    {
        var provider = DataProtectionProvider.Create("BlazorBase.DataProtection.Test.PurposedVersusDefault");
        BlazorBaseDataProtection.Initialize(provider);

        var purposedCiphertext = BlazorBaseDataProtection.Protect("purposed path secret", "email");

        Assert.Throws<CryptographicException>(() => BlazorBaseDataProtection.Unprotect(purposedCiphertext));
    }

    [Fact]
    public void Protect_WithEmptyPurpose_ThrowsArgumentException()
    {
        var provider = DataProtectionProvider.Create("BlazorBase.DataProtection.Test.EmptyPurpose");
        BlazorBaseDataProtection.Initialize(provider);

        Assert.Throws<ArgumentException>(() => BlazorBaseDataProtection.Protect("text", string.Empty));
    }

    [Fact]
    public void Protect_WithNullPurpose_ThrowsArgumentException()
    {
        var provider = DataProtectionProvider.Create("BlazorBase.DataProtection.Test.NullPurpose");
        BlazorBaseDataProtection.Initialize(provider);

        Assert.Throws<ArgumentException>(() => BlazorBaseDataProtection.Protect("text", null!));
    }

    [Fact]
    public void Protect_WithPurpose_BeforeInitialize_ThrowsInvalidOperationException()
    {
        BlazorBaseDataProtection.ResetForTesting();

        try
        {
            Assert.Throws<InvalidOperationException>(() => BlazorBaseDataProtection.Protect("text", "email"));
        }
        finally
        {
            BlazorBaseDataProtection.Initialize(DataProtectionProvider.Create("BlazorBase.DataProtection.Test.Reset.Protect"));
        }
    }

    [Fact]
    public void Unprotect_WithPurpose_BeforeInitialize_ThrowsInvalidOperationException()
    {
        BlazorBaseDataProtection.ResetForTesting();

        try
        {
            Assert.Throws<InvalidOperationException>(() => BlazorBaseDataProtection.Unprotect("ciphertext", "email"));
        }
        finally
        {
            BlazorBaseDataProtection.Initialize(DataProtectionProvider.Create("BlazorBase.DataProtection.Test.Reset.Unprotect"));
        }
    }
}
