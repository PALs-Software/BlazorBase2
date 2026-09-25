using BlazorBase.Speech.Services;

namespace BlazorBase.Speech.Test.Services;

/// <summary>Audio player double that logs what happens and lets a test decide when a clip ends.</summary>
public sealed class RecordingAudioPlayer(List<string> journal) : ISpeechAudioPlayer
{
    #region Injects
    private readonly List<string> Journal = journal;
    #endregion

    public Func<byte[], CancellationToken, Task<bool>> Playback { get; set; } = (_, _) => Task.FromResult(true);

    public int StopCount { get; private set; }

    public ValueTask PrimeAsync() => ValueTask.CompletedTask;

    public async ValueTask<bool> PlayAsync(byte[] wav, CancellationToken cancellationToken = default)
    {
        Journal.Add($"play:{System.Text.Encoding.UTF8.GetString(wav)}");
        return await Playback(wav, cancellationToken);
    }

    public ValueTask StopAsync()
    {
        StopCount++;
        return ValueTask.CompletedTask;
    }
}
