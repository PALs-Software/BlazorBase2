namespace BlazorBase.Speech.Server.Contracts;

/// <summary>Request body of the speech service's <c>POST /v1/audio/speech</c>, serialized in camelCase.</summary>
/// <param name="Input">The text to speak.</param>
/// <param name="Language">Two-letter language code the service maps to a voice.</param>
/// <param name="Voice">A specific voice; wins over <paramref name="Language"/>. Omitted from the JSON when <see langword="null"/>.</param>
public record SpeechApiSpeechRequest(
    string Input,
    string? Language,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Voice = null);
