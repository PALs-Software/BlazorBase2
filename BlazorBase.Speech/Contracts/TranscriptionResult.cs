namespace BlazorBase.Speech.Contracts;

/// <summary>Text recognized in a recording. Empty when nothing intelligible was said.</summary>
/// <param name="Text">The recognized text, trimmed.</param>
public record TranscriptionResult(string Text);
