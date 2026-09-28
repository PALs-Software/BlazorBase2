namespace BlazorBase.Speech.Server.Contracts;

/// <summary>Response body of the speech service's <c>POST /v1/audio/transcriptions</c>.</summary>
/// <param name="Text">The recognized text.</param>
public record SpeechApiTranscriptionResponse(string? Text);
