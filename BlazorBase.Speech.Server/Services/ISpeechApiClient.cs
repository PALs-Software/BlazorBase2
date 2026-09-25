namespace BlazorBase.Speech.Server.Services;

/// <summary>
/// Client for the internal speech service (the SpeechApi container, OpenAI-shaped audio routes).
/// Failures surface as <see cref="BlazorBase.Speech.Services.SpeechRequestException"/>.
/// </summary>
public interface ISpeechApiClient
{
    /// <summary>Transcribes 16-bit PCM WAV audio.</summary>
    Task<string> TranscribeAsync(Stream wav, string? language, CancellationToken cancellationToken);

    /// <summary>Synthesizes WAV audio for plain text.</summary>
    Task<byte[]> SynthesizeAsync(string text, string? language, CancellationToken cancellationToken);
}
