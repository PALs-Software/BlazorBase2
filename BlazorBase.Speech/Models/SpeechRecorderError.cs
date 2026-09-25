namespace BlazorBase.Speech.Models;

/// <summary>Why the browser could not record.</summary>
public enum SpeechRecorderError
{
    /// <summary>The user or a browser policy denied microphone access.</summary>
    PermissionDenied,

    /// <summary>No microphone is connected, or none satisfies the requested constraints.</summary>
    NoMicrophone,

    /// <summary>The browser lacks <c>getUserMedia</c> or <c>AudioWorklet</c>, or the page is not served over HTTPS.</summary>
    Unsupported,

    /// <summary>Recording failed for any other reason.</summary>
    Failed
}
