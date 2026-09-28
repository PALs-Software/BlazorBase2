namespace BlazorBase.Speech.Contracts;

/// <summary>Constants describing the audio format exchanged by the speech module.</summary>
public static class SpeechAudio
{
    /// <summary>Media type of every recording and every synthesized clip.</summary>
    public const string WavContentType = "audio/wav";

    /// <summary>Sample rate recordings are captured at. Speech recognition models work at 16 kHz.</summary>
    public const int RecordingSampleRate = 16000;

    /// <summary>Bytes per sample of a recording (16-bit PCM, mono).</summary>
    public const int RecordingBytesPerSample = 2;

    /// <summary>Size of the canonical RIFF/WAVE header written in front of a recording.</summary>
    public const int WavHeaderLength = 44;
}
