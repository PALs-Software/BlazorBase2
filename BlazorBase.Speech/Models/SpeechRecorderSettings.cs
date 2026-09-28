namespace BlazorBase.Speech.Models;

/// <summary>Limits handed to the browser-side recorder.</summary>
/// <param name="MaximumDurationMilliseconds">Recording stops by itself after this long.</param>
/// <param name="MinimumDurationMilliseconds">Shorter presses are discarded as accidental taps.</param>
public record SpeechRecorderSettings(int MaximumDurationMilliseconds, int MinimumDurationMilliseconds);
