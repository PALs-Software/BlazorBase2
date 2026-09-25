using System.Net;
using System.Net.Http.Json;
using System.Text;
using BlazorBase.Speech.Contracts;
using BlazorBase.Speech.Models;
using BlazorBase.Speech.Services;
using BlazorBase.Speech.Test.Infrastructure;
using Xunit;

namespace BlazorBase.Speech.Test.Services;

public sealed class HttpSpeechClientTests
{
    private static (HttpSpeechClient Client, StubHttpMessageHandler Handler) CreateClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
    {
        var handler = new StubHttpMessageHandler(respond);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://portal.test/api/speech/") };
        return (new HttpSpeechClient(httpClient), handler);
    }

    private static Task<HttpResponseMessage> Json<T>(T value)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(value) });

    private static Task<HttpResponseMessage> Status(HttpStatusCode statusCode)
        => Task.FromResult(new HttpResponseMessage(statusCode));

    [Fact]
    public async Task TranscribeAsync_PostsTheRecordingAndLanguageAsMultipart()
    {
        var (client, handler) = CreateClient(_ => Json(new TranscriptionResult("Hallo Welt")));

        var text = await client.TranscribeAsync(Encoding.ASCII.GetBytes("RIFF-audio"), "de");

        Assert.Equal("Hallo Welt", text);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://portal.test/api/speech/transcriptions", request.RequestUri!.ToString());
        Assert.Contains("name=file; filename=recording.wav", handler.RequestBodies[0]);
        Assert.Contains("audio/wav", handler.RequestBodies[0]);
        Assert.Contains("RIFF-audio", handler.RequestBodies[0]);
        Assert.Contains("name=language", handler.RequestBodies[0]);
        Assert.Contains("\r\nde\r\n", handler.RequestBodies[0]);
    }

    [Fact]
    public async Task TranscribeAsync_WithoutLanguage_LetsTheServiceDetectIt_InsteadOfForcingTheUiCulture()
    {
        using var culture = new CultureScope("de-DE");
        var (client, handler) = CreateClient(_ => Json(new TranscriptionResult("x")));

        await client.TranscribeAsync([1, 2, 3]);

        Assert.DoesNotContain("name=language", handler.RequestBodies[0]);
    }

    [Fact]
    public async Task SynthesizeAsync_WithoutLanguage_FallsBackToTheUiCulture()
    {
        using var culture = new CultureScope("en-GB");
        var (client, handler) = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) }));

        await client.SynthesizeAsync("Hello");

        Assert.Contains("\"language\":\"en\"", handler.RequestBodies[0]);
    }

    [Fact]
    public async Task SynthesizeAsync_PostsJsonAndReturnsTheAudio()
    {
        var (client, handler) = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([9, 8, 7])
        }));

        var audio = await client.SynthesizeAsync("Guten Tag", "de");

        Assert.Equal([9, 8, 7], audio);
        Assert.Equal("https://portal.test/api/speech/speech", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("\"text\":\"Guten Tag\"", handler.RequestBodies[0]);
        Assert.Contains("\"language\":\"de\"", handler.RequestBodies[0]);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, SpeechFailureKind.Busy)]
    [InlineData(HttpStatusCode.ServiceUnavailable, SpeechFailureKind.Busy)]
    [InlineData(HttpStatusCode.GatewayTimeout, SpeechFailureKind.Timeout)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, SpeechFailureKind.Rejected)]
    [InlineData(HttpStatusCode.UnsupportedMediaType, SpeechFailureKind.Rejected)]
    [InlineData(HttpStatusCode.BadRequest, SpeechFailureKind.Rejected)]
    [InlineData(HttpStatusCode.BadGateway, SpeechFailureKind.Unavailable)]
    [InlineData(HttpStatusCode.InternalServerError, SpeechFailureKind.Unavailable)]
    public async Task FailedStatus_MapsToAFailureKind(HttpStatusCode statusCode, SpeechFailureKind expected)
    {
        var (client, _) = CreateClient(_ => Status(statusCode));

        var exception = await Assert.ThrowsAsync<SpeechRequestException>(() => client.SynthesizeAsync("x", "de"));

        Assert.Equal(expected, exception.Kind);
    }

    [Fact]
    public async Task UnreachableHost_IsUnavailable()
    {
        var (client, _) = CreateClient(_ => throw new HttpRequestException("refused"));

        var exception = await Assert.ThrowsAsync<SpeechRequestException>(() => client.TranscribeAsync([1], "de"));

        Assert.Equal(SpeechFailureKind.Unavailable, exception.Kind);
    }

    [Fact]
    public async Task HttpClientTimeout_IsTimeout()
    {
        var (client, _) = CreateClient(_ => throw new TaskCanceledException("timeout"));

        var exception = await Assert.ThrowsAsync<SpeechRequestException>(() => client.TranscribeAsync([1], "de"));

        Assert.Equal(SpeechFailureKind.Timeout, exception.Kind);
    }

    [Fact]
    public async Task CallerCancellation_IsNotRewritten()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var (client, _) = CreateClient(_ => throw new TaskCanceledException("cancelled"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.TranscribeAsync([1], "de", cancellation.Token));
    }

    [Fact]
    public async Task IsAvailableAsync_ReadsTheFlag()
    {
        var (enabled, _) = CreateClient(_ => Json(new SpeechAvailability(true)));
        var (disabled, _) = CreateClient(_ => Json(new SpeechAvailability(false)));

        Assert.True(await enabled.IsAvailableAsync());
        Assert.False(await disabled.IsAvailableAsync());
    }

    [Fact]
    public async Task IsAvailableAsync_AnythingButTheContract_IsUnavailable()
    {
        var (notFound, _) = CreateClient(_ => Status(HttpStatusCode.NotFound));
        var (fallbackPage, _) = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<!DOCTYPE html><html></html>", Encoding.UTF8, "text/html")
        }));
        var (unreachable, _) = CreateClient(_ => throw new HttpRequestException("refused"));

        Assert.False(await notFound.IsAvailableAsync());
        Assert.False(await fallbackPage.IsAvailableAsync());
        Assert.False(await unreachable.IsAvailableAsync());
    }
}
