using Microsoft.JSInterop;

namespace BlazorBase.Speech.Services;

/// <summary>
/// <see cref="ISpeechAudioPlayer"/> over the Web Audio API. Audio travels to the browser as a
/// <see cref="DotNetStreamReference"/>, so clips are streamed rather than serialized - which keeps
/// Blazor Server within its SignalR message size limit as well.
/// </summary>
public sealed class JsSpeechAudioPlayer(IJSRuntime jsRuntime) : ISpeechAudioPlayer, IAsyncDisposable
{
    #region Injects
    private readonly IJSRuntime JsRuntime = jsRuntime;
    #endregion

    /// <summary>Path of the JS module, shared with the recorder, which primes playback on every press.</summary>
    public const string ModulePath = "./_content/BlazorBase.Speech/js/speechPlayer.js";

    private Task<IJSObjectReference>? ModuleTask;

    /// <inheritdoc/>
    public async ValueTask PrimeAsync()
    {
        var module = await GetModuleAsync().ConfigureAwait(false);
        await module.InvokeVoidAsync("primePlayback").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> PlayAsync(byte[] wav, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var module = await GetModuleAsync().ConfigureAwait(false);

        using var audioStream = new MemoryStream(wav, writable: false);
        using var streamReference = new DotNetStreamReference(audioStream, leaveOpen: false);
        await using var stopOnCancel = cancellationToken.Register(() => _ = StopAsync().AsTask());

        return await module.InvokeAsync<bool>("play", streamReference).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask StopAsync()
    {
        if (ModuleTask is null)
            return;

        var module = await ModuleTask.ConfigureAwait(false);
        await module.InvokeVoidAsync("stop").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (ModuleTask is null)
            return;

        try
        {
            var module = await ModuleTask.ConfigureAwait(false);
            await module.InvokeVoidAsync("stop").ConfigureAwait(false);
            await module.DisposeAsync().ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private Task<IJSObjectReference> GetModuleAsync()
        => ModuleTask ??= JsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();
}
