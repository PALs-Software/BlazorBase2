namespace BlazorBase.Speech.Contracts;

/// <summary>Whether speech input and output can be offered at all.</summary>
/// <param name="IsAvailable"><see langword="false"/> when the host has no speech service configured.</param>
public record SpeechAvailability(bool IsAvailable);
