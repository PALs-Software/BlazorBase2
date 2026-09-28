namespace BlazorBase.Speech.Models;

/// <summary>Why a transcription or synthesis request failed, reduced to what a user can act on.</summary>
public enum SpeechFailureKind
{
    /// <summary>The speech service is down or unreachable.</summary>
    Unavailable,

    /// <summary>The speech service is busy with other requests; trying again shortly helps.</summary>
    Busy,

    /// <summary>The speech service did not answer in time.</summary>
    Timeout,

    /// <summary>The request itself was refused, for example a recording that is too long.</summary>
    Rejected
}
