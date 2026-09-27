namespace BlazorBase.Speech.Contracts;

/// <summary>Plain text to read aloud.</summary>
/// <param name="Text">The text to speak. Markdown should already be removed, see <c>SpeechTextPreparer</c>.</param>
/// <param name="Language">Two-letter language code selecting the voice, for example <c>de</c> or <c>en</c>.</param>
/// <param name="Voice">
/// A specific voice from the <see cref="SpeechVoiceCatalog"/>; it wins over <paramref name="Language"/>. When
/// <see langword="null"/>, the service picks its default voice for the language.
/// </param>
public record SpeechSynthesisRequest(string Text, string? Language, string? Voice = null);
