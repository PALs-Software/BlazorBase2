namespace BlazorBase.Speech.Services;

/// <summary>
/// Talks to the host's speech proxy (a controller derived from <c>SpeechControllerBase</c>).
/// Every failure surfaces as a <see cref="SpeechRequestException"/>.
/// </summary>
public interface ISpeechClient
{
    /// <summary>
    /// Asks the host whether a speech service is configured, so a UI can hide speech features
    /// instead of offering something that cannot work. Returns <see langword="false"/> on any failure.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>Transcribes a WAV recording.</summary>
    /// <param name="wav">16-bit PCM WAV audio.</param>
    /// <param name="language">
    /// Two-letter language code of what was said. Leave it <see langword="null"/> unless it is really known:
    /// the speech service then detects the language itself. A wrong code is worse than none - speech recognition
    /// translates into the given language instead of transcribing.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The recognized text, empty when nothing intelligible was said.</returns>
    Task<string> TranscribeAsync(byte[] wav, string? language = null, CancellationToken cancellationToken = default);

    /// <summary>Synthesizes speech for plain text.</summary>
    /// <param name="text">The text to speak.</param>
    /// <param name="language">Two-letter language code; the current UI culture when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>WAV audio.</returns>
    Task<byte[]> SynthesizeAsync(string text, string? language = null, CancellationToken cancellationToken = default);
}
