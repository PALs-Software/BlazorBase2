namespace BlazorBase.Speech.Server.Configuration;

/// <summary>Where the speech service runs and how much a single user may ask of it.</summary>
public class SpeechServerOptions
{
    /// <summary>Default configuration section.</summary>
    public const string SectionName = "Speech";

    /// <summary>
    /// Base address of the speech service (for example <c>http://speech-api:8080</c>). Leave empty to
    /// switch speech off: the proxy then reports itself unavailable instead of failing at startup.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>How long one transcription or synthesis may take. Defaults to 120 seconds.</summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>Largest accepted recording. Defaults to 10 MiB, about five minutes of 16 kHz mono audio.</summary>
    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Longest text accepted for one synthesis request. Defaults to 4000 characters.</summary>
    public int MaxTextCharacters { get; set; } = 4000;

    /// <summary>
    /// Requests one user may have in flight at once. Defaults to 2: one transcription plus one synthesis,
    /// or a synthesis plus the prefetch of the next segment.
    /// </summary>
    public int MaxConcurrentRequestsPerUser { get; set; } = 2;

    /// <summary><see langword="true"/> when <see cref="BaseUrl"/> is set.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl);
}
