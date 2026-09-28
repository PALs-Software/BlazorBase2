namespace BlazorBase.Speech.Services;

/// <summary>Plays synthesized clips in the browser, one at a time.</summary>
public interface ISpeechAudioPlayer
{
    /// <summary>
    /// Unlocks audio output. Browsers - Safari on iOS in particular - only allow playback that was
    /// started by a user gesture, so call this from one (the push-to-talk button does).
    /// </summary>
    ValueTask PrimeAsync();

    /// <summary>Plays one WAV clip, replacing whatever is playing.</summary>
    /// <param name="wav">WAV audio.</param>
    /// <param name="cancellationToken">Stops playback when cancelled.</param>
    /// <returns><see langword="true"/> when the clip played to the end, <see langword="false"/> when it was stopped.</returns>
    ValueTask<bool> PlayAsync(byte[] wav, CancellationToken cancellationToken = default);

    /// <summary>Stops the current clip, if any.</summary>
    ValueTask StopAsync();
}
