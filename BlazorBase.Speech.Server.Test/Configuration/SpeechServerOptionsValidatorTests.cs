using BlazorBase.Speech.Server.Configuration;
using Xunit;

namespace BlazorBase.Speech.Server.Test.Configuration;

public sealed class SpeechServerOptionsValidatorTests
{
    private static bool IsValid(SpeechServerOptions options)
        => new SpeechServerOptionsValidator().Validate(null, options).Succeeded;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://speech-api:8080")]
    [InlineData("https://speech.example.com/base/")]
    public void AcceptsAnEmptyOrAbsoluteHttpUrl(string? baseUrl)
        => Assert.True(IsValid(new SpeechServerOptions { BaseUrl = baseUrl }));

    [Theory]
    [InlineData("speech-api:8080")]
    [InlineData("/relative")]
    [InlineData("tcp://speech-whisper:10300")]
    public void RejectsAnythingElse(string baseUrl)
        => Assert.False(IsValid(new SpeechServerOptions { BaseUrl = baseUrl }));

    [Fact]
    public void RejectsNonPositiveLimits()
    {
        Assert.False(IsValid(new SpeechServerOptions { TimeoutSeconds = 0 }));
        Assert.False(IsValid(new SpeechServerOptions { MaxUploadBytes = 0 }));
        Assert.False(IsValid(new SpeechServerOptions { MaxTextCharacters = -1 }));
        Assert.False(IsValid(new SpeechServerOptions { MaxConcurrentRequestsPerUser = 0 }));
    }
}
