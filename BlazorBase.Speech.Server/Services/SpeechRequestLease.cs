namespace BlazorBase.Speech.Server.Services;

/// <summary>A claimed <see cref="SpeechRequestGate"/> slot; disposing it frees the slot exactly once.</summary>
public sealed class SpeechRequestLease(Action release) : IDisposable
{
    #region Injects
    private readonly Action Release = release;
    #endregion

    private int IsReleased;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref IsReleased, 1) == 1)
            return;

        Release();
    }
}
