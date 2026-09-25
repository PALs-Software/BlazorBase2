using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BlazorBase.Speech.Contracts;
using BlazorBase.Speech.Models;
using BlazorBase.Speech.Server.Contracts;
using BlazorBase.Speech.Services;

namespace BlazorBase.Speech.Server.Services;

/// <summary>
/// Typed <see cref="HttpClient"/> for the speech service. Base address and timeout come from
/// <c>SpeechServerOptions</c>, set up by <c>AddBlazorBaseSpeechServer</c>.
/// </summary>
public class SpeechApiClient(HttpClient httpClient) : ISpeechApiClient
{
    #region Injects
    private readonly HttpClient HttpClient = httpClient;
    #endregion

    private const string TranscriptionsRoute = "v1/audio/transcriptions";
    private const string SpeechRoute = "v1/audio/speech";
    private const string RecordingFileName = "recording.wav";

    /// <inheritdoc/>
    public async Task<string> TranscribeAsync(Stream wav, string? language, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        var audioContent = new StreamContent(wav);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue(SpeechAudio.WavContentType);
        form.Add(audioContent, "file", RecordingFileName);

        if (!string.IsNullOrWhiteSpace(language))
            form.Add(new StringContent(language), "language");

        using var response = await SendAsync(() => HttpClient.PostAsync(TranscriptionsRoute, form, cancellationToken), cancellationToken).ConfigureAwait(false);
        var transcription = await response.Content.ReadFromJsonAsync<SpeechApiTranscriptionResponse>(cancellationToken).ConfigureAwait(false);
        return transcription?.Text?.Trim() ?? string.Empty;
    }

    /// <inheritdoc/>
    public async Task<byte[]> SynthesizeAsync(string text, string? language, CancellationToken cancellationToken)
    {
        var request = new SpeechApiSpeechRequest(text, language);

        using var response = await SendAsync(() => HttpClient.PostAsJsonAsync(SpeechRoute, request, cancellationToken), cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await send().ConfigureAwait(false);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SpeechRequestException(SpeechFailureKind.Timeout, "The speech service did not answer in time.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new SpeechRequestException(SpeechFailureKind.Unavailable, "The speech service could not be reached.", exception);
        }

        if (response.IsSuccessStatusCode)
            return response;

        var statusCode = response.StatusCode;
        response.Dispose();
        throw new SpeechRequestException(ClassifyFailure(statusCode), $"The speech service answered {(int)statusCode}.");
    }

    private static SpeechFailureKind ClassifyFailure(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests => SpeechFailureKind.Busy,
        HttpStatusCode.GatewayTimeout => SpeechFailureKind.Timeout,
        HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.UnsupportedMediaType => SpeechFailureKind.Rejected,
        _ => SpeechFailureKind.Unavailable
    };
}
