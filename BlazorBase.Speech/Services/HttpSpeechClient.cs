using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BlazorBase.Speech.Contracts;
using BlazorBase.Speech.Models;

namespace BlazorBase.Speech.Services;

/// <summary>
/// HTTP implementation of <see cref="ISpeechClient"/>. The injected <see cref="HttpClient"/> must be
/// configured by the host with a base address pointing at the proxy (<c>…/api/speech/</c>, trailing
/// slash included) and its authentication handler, typically <c>AuthTokenHandler</c>; this class
/// contains no auth logic.
/// </summary>
public class HttpSpeechClient(HttpClient httpClient) : ISpeechClient
{
    #region Injects
    private readonly HttpClient HttpClient = httpClient;
    #endregion

    private const string RecordingFileName = "recording.wav";

    /// <inheritdoc/>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var availability = await HttpClient.GetFromJsonAsync<SpeechAvailability>(SpeechRoutes.Availability, cancellationToken).ConfigureAwait(false);
            return availability?.IsAvailable ?? false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<string> TranscribeAsync(byte[] wav, string? language = null, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        var audioContent = new ByteArrayContent(wav);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue(SpeechAudio.WavContentType);
        form.Add(audioContent, "file", RecordingFileName);

        if (!string.IsNullOrWhiteSpace(language))
            form.Add(new StringContent(language), "language");

        using var response = await SendAsync(() => HttpClient.PostAsync(SpeechRoutes.Transcriptions, form, cancellationToken), cancellationToken).ConfigureAwait(false);
        var result = await response.Content.ReadFromJsonAsync<TranscriptionResult>(cancellationToken).ConfigureAwait(false);
        return result?.Text ?? string.Empty;
    }

    /// <inheritdoc/>
    public async Task<byte[]> SynthesizeAsync(string text, string? language = null, CancellationToken cancellationToken = default)
    {
        var request = new SpeechSynthesisRequest(text, ResolveLanguage(language));

        using var response = await SendAsync(() => HttpClient.PostAsJsonAsync(SpeechRoutes.Speech, request, cancellationToken), cancellationToken).ConfigureAwait(false);
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
            throw new SpeechRequestException(SpeechFailureKind.Timeout, "The speech request timed out.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new SpeechRequestException(SpeechFailureKind.Unavailable, "The speech service could not be reached.", exception);
        }

        if (response.IsSuccessStatusCode)
            return response;

        var kind = ClassifyFailure(response.StatusCode);
        var statusCode = (int)response.StatusCode;
        response.Dispose();
        throw new SpeechRequestException(kind, $"The speech request failed with status {statusCode}.");
    }

    private static SpeechFailureKind ClassifyFailure(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable => SpeechFailureKind.Busy,
        HttpStatusCode.GatewayTimeout or HttpStatusCode.RequestTimeout => SpeechFailureKind.Timeout,
        HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.UnsupportedMediaType => SpeechFailureKind.Rejected,
        _ => SpeechFailureKind.Unavailable
    };

    private static string ResolveLanguage(string? language)
        => string.IsNullOrWhiteSpace(language)
            ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            : language;
}
