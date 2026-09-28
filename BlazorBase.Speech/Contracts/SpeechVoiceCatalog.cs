namespace BlazorBase.Speech.Contracts;

/// <summary>The voices a speech service offers, and which ones it uses when a request names none.</summary>
/// <param name="Voices">The available voices, sorted by name.</param>
/// <param name="DefaultVoices">The voice used per two-letter language code when a request names no voice.</param>
/// <param name="DefaultVoice">The voice used when neither a voice nor a known language is given.</param>
public record SpeechVoiceCatalog(
    IReadOnlyList<SpeechVoice> Voices,
    IReadOnlyDictionary<string, string> DefaultVoices,
    string? DefaultVoice);
