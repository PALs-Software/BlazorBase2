namespace BlazorBase.Speech.Models;

/// <summary>A finished push-to-talk recording.</summary>
/// <param name="Wav">16 kHz, 16-bit, mono PCM audio including its RIFF/WAVE header.</param>
/// <param name="Duration">How long the button was held, as measured from the captured samples.</param>
public record SpeechRecording(byte[] Wav, TimeSpan Duration);
