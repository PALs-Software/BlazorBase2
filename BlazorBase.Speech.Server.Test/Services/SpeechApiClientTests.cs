using System.Net;
using System.Net.Http.Json;
using BlazorBase.Speech.Models;
using BlazorBase.Speech.Server.Services;
using BlazorBase.Speech.Server.Test.Infrastructure;
using BlazorBase.Speech.Services;
using Xunit;

namespace BlazorBase.Speech.Server.Test.Services;

public sealed class SpeechApiClientTests
{
    private static (SpeechApiClient Client, StubHttpMessageHandler Handler) CreateClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
    {
        var handler = new StubHttpMessageHandler(respond);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://speech-api:8080/") };
        return (new SpeechApiClient(httpClient), handler);
    }

    [Fact]
    public async Task TranscribeAsync_UsesTheOpenAiShapedRoute()
    {
        var (client, handler) = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { text = "  Hallo Welt \n" })
        }));

        var text = await client.TranscribeAsync(new MemoryStream("RIFF"u8.ToArray()), "de", CancellationToken.None);

        Assert.Equal("Hallo Welt", text);
        Assert.Equal("http://speech-api:8080/v1/audio/transcriptions", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("name=file; filename=recording.wav", handler.RequestBodies[0]);
        Assert.Contains("Content-Type: audio/wav", handler.RequestBodies[0]);
        Assert.Contains("name=language", handler.RequestBodies[0]);
    }

    [Fact]
    public async Task TranscribeAsync_WithoutLanguage_SendsNoLanguageField()
    {
        var (client, handler) = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { text = "x" })
        }));

        await client.TranscribeAsync(new MemoryStream([1]), null, CancellationToken.None);

        Assert.DoesNotContain("name=language", handler.RequestBodies[0]);
    }

    [Fact]
    public async Task SynthesizeAsync_SendsInputInCamelCase()
    {
        var (client, handler) = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([4, 5])
        }));

        var audio = await client.SynthesizeAsync("Guten Tag", "de", CancellationToken.None);

        Assert.Equal([4, 5], audio);
        Assert.Equal("http://speech-api:8080/v1/audio/speech", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("\"input\":\"Guten Tag\"", handler.RequestBodies[0]);
        Assert.Contains("\"language\":\"de\"", handler.RequestBodies[0]);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, SpeechFailureKind.Busy)]
    [InlineData(HttpStatusCode.GatewayTimeout, SpeechFailureKind.Timeout)]
    [InlineData(HttpStatusCode.UnsupportedMediaType, SpeechFailureKind.Rejected)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, SpeechFailureKind.Rejected)]
    [InlineData(HttpStatusCode.BadGateway, SpeechFailureKind.Unavailable)]
    public async Task FailedStatus_MapsToAFailureKind(HttpStatusCode statusCode, SpeechFailureKind expected)
    {
        var (client, _) = CreateClient(_ => Task.FromResult(new HttpResponseMessage(statusCode)));

        var exception = await Assert.ThrowsAsync<SpeechRequestException>(() => client.SynthesizeAsync("x", "de", CancellationToken.None));

        Assert.Equal(expected, exception.Kind);
    }

    [Fact]
    public async Task ConnectionFailureAndTimeout_AreClassified()
    {
        var (refused, _) = CreateClient(_ => throw new HttpRequestException("refused"));
        var (slow, _) = CreateClient(_ => throw new TaskCanceledException("timeout"));

        var unavailable = await Assert.ThrowsAsync<SpeechRequestException>(() => refused.SynthesizeAsync("x", "de", CancellationToken.None));
        var timeout = await Assert.ThrowsAsync<SpeechRequestException>(() => slow.SynthesizeAsync("x", "de", CancellationToken.None));

        Assert.Equal(SpeechFailureKind.Unavailable, unavailable.Kind);
        Assert.Equal(SpeechFailureKind.Timeout, timeout.Kind);
    }
}
