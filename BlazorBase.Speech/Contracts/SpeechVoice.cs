namespace BlazorBase.Speech.Contracts;

/// <summary>A voice the speech service can read with.</summary>
/// <param name="Name">The voice's identifier, for example <c>de_DE-thorsten-high</c>; pass it as the voice of a synthesis request.</param>
/// <param name="Language">The voice's language tag, for example <c>de_DE</c>.</param>
/// <param name="Description">A human-readable description, when the service provides one.</param>
public record SpeechVoice(string Name, string Language, string? Description);
