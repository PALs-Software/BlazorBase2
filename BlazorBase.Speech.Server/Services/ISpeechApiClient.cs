using BlazorBase.Speech.Contracts;

namespace BlazorBase.Speech.Server.Services;

/// <summary>
/// Client for the internal speech service (the SpeechApi container, OpenAI-shaped audio routes).
/// Failures surface as <see cref="BlazorBase.Speech.Services.SpeechRequestException"/>.
/// </summary>
public interface ISpeechApiClient
{
    /// <summary>Transcribes 16-bit PCM WAV audio.</summary>
    Task<string> TranscribeAsync(Stream wav, string? language, CancellationToken cancellationToken);

    /// <summary>Synthesizes WAV audio for plain text, with a specific voice when <paramref name="voice"/> is set.</summary>
    Task<byte[]> SynthesizeAsync(string text, string? language, string? voice, CancellationToken cancellationToken);

    /// <summary>Lists the voices of the service, optionally only those of one language.</summary>
    Task<SpeechVoiceCatalog> GetVoicesAsync(string? language, CancellationToken cancellationToken);
}
