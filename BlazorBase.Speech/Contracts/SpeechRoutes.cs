namespace BlazorBase.Speech.Contracts;

/// <summary>
/// Routes of the speech proxy a host exposes by deriving <c>SpeechControllerBase</c>. Shared by the
/// server controller and <see cref="Services.HttpSpeechClient"/> so both sides cannot drift apart.
/// </summary>
public static class SpeechRoutes
{
    /// <summary>Base route of the proxy controller, relative to the host's root.</summary>
    public const string Base = "api/speech";

    /// <summary>Transcribes an uploaded WAV recording (multipart form, field <c>file</c>).</summary>
    public const string Transcriptions = "transcriptions";

    /// <summary>Synthesizes speech for a <see cref="SpeechSynthesisRequest"/> and returns WAV audio.</summary>
    public const string Speech = "speech";

    /// <summary>Reports whether the host has a speech service configured, see <see cref="SpeechAvailability"/>.</summary>
    public const string Availability = "availability";
}
