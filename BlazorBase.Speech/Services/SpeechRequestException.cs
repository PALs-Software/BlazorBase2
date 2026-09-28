using BlazorBase.Speech.Models;

namespace BlazorBase.Speech.Services;

/// <summary>Thrown by <see cref="ISpeechClient"/> when the host's speech proxy refuses or fails a request.</summary>
public class SpeechRequestException(SpeechFailureKind kind, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>What went wrong, suitable for choosing a message to show.</summary>
    public SpeechFailureKind Kind { get; } = kind;
}
