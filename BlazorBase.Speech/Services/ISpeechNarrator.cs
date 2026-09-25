namespace BlazorBase.Speech.Services;

/// <summary>Reads text aloud: prepares it, synthesizes it segment by segment and plays it.</summary>
public interface ISpeechNarrator
{
    /// <summary><see langword="true"/> while something is being read aloud.</summary>
    bool IsSpeaking { get; }

    /// <summary>Raised whenever <see cref="IsSpeaking"/> changes.</summary>
    event Action? SpeakingChanged;

    /// <summary>
    /// Reads <paramref name="text"/> aloud, stopping whatever was being read before. Completes when the
    /// last segment has played or when reading was stopped.
    /// </summary>
    /// <param name="text">Text as written in the chat; Markdown is removed before synthesis.</param>
    /// <param name="language">
    /// Two-letter language code choosing the voice. When <see langword="null"/>, the language is guessed from the
    /// text (<see cref="Text.SpeechLanguageGuesser"/>), and only if that does not tell, the UI culture is used.
    /// </param>
    /// <param name="cancellationToken">Stops reading when cancelled.</param>
    /// <exception cref="SpeechRequestException">Synthesis failed before anything could be played.</exception>
    Task SpeakAsync(string text, string? language = null, CancellationToken cancellationToken = default);

    /// <summary>Stops reading immediately.</summary>
    Task StopAsync();
}
