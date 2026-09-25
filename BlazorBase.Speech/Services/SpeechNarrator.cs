using BlazorBase.Speech.Text;

namespace BlazorBase.Speech.Services;

/// <summary>
/// Default <see cref="ISpeechNarrator"/>. While one segment plays, the next one is already being
/// synthesized, so the pauses between segments stay short without synthesizing a long answer up front.
/// </summary>
public sealed class SpeechNarrator(
    ISpeechClient speechClient,
    ISpeechAudioPlayer audioPlayer,
    SpeechTextPreparer textPreparer) : ISpeechNarrator, IDisposable
{
    #region Injects
    private readonly ISpeechClient SpeechClient = speechClient;
    private readonly ISpeechAudioPlayer AudioPlayer = audioPlayer;
    private readonly SpeechTextPreparer TextPreparer = textPreparer;
    #endregion

    private CancellationTokenSource? CurrentReading;

    /// <inheritdoc/>
    public bool IsSpeaking => CurrentReading is not null;

    /// <inheritdoc/>
    public event Action? SpeakingChanged;

    /// <inheritdoc/>
    public async Task SpeakAsync(string text, string? language = null, CancellationToken cancellationToken = default)
    {
        await StopAsync().ConfigureAwait(false);

        var voiceLanguage = language ?? SpeechLanguageGuesser.Guess(text);
        var segments = TextPreparer.Prepare(text, voiceLanguage);

        if (segments.Count == 0)
            return;

        using var reading = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CurrentReading = reading;
        SpeakingChanged?.Invoke();

        try
        {
            await ReadSegmentsAsync(segments, voiceLanguage, reading.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (reading.IsCancellationRequested)
        {
        }
        finally
        {
            reading.Cancel();

            if (ReferenceEquals(CurrentReading, reading))
            {
                CurrentReading = null;
                SpeakingChanged?.Invoke();
            }
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        var reading = CurrentReading;

        if (reading is null)
            return;

        TryCancel(reading);
        await AudioPlayer.StopAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose() => TryCancel(CurrentReading);

    private static void TryCancel(CancellationTokenSource? reading)
    {
        if (reading is null)
            return;

        try
        {
            reading.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task ReadSegmentsAsync(IReadOnlyList<string> segments, string? language, CancellationToken cancellationToken)
    {
        var pendingAudio = SpeechClient.SynthesizeAsync(segments[0], language, cancellationToken);

        for (var index = 0; index < segments.Count; index++)
        {
            var audio = await pendingAudio.ConfigureAwait(false);

            if (index + 1 < segments.Count)
                pendingAudio = SpeechClient.SynthesizeAsync(segments[index + 1], language, cancellationToken);

            var playedToEnd = await AudioPlayer.PlayAsync(audio, cancellationToken).ConfigureAwait(false);

            if (playedToEnd && !cancellationToken.IsCancellationRequested)
                continue;

            ObserveAbandoned(pendingAudio);
            return;
        }
    }

    private static void ObserveAbandoned(Task pendingAudio)
        => pendingAudio.ContinueWith(
            abandoned => abandoned.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
}
