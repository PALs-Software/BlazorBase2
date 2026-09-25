using BlazorBase.Speech.Components.PushToTalk;
using BlazorBase.Speech.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBase.Speech.Interop;

/// <summary>
/// JS-module wrapper for <c>speechRecorder.js</c>. The recording is pulled as an
/// <see cref="IJSStreamReference"/> instead of being serialized as one argument, so it works under
/// Blazor Server's SignalR message size limit as well as under WebAssembly.
/// </summary>
public sealed class SpeechRecorderInterop(IJSRuntime jsRuntime) : ISpeechRecorderInterop
{
    #region Injects
    private readonly IJSRuntime JsRuntime = jsRuntime;
    #endregion

    private const string ModulePath = "./_content/BlazorBase.Speech/js/speechRecorder.js";

    private Task<IJSObjectReference>? ModuleTask;

    /// <inheritdoc/>
    public async ValueTask AttachAsync(ElementReference button, DotNetObjectReference<PushToTalkButton> callbackTarget, SpeechRecorderSettings settings)
    {
        var module = await GetModuleAsync().ConfigureAwait(false);
        await module.InvokeVoidAsync("attach", button, callbackTarget, settings).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<byte[]> TakeRecordingAsync(ElementReference button, long maximumBytes, CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync().ConfigureAwait(false);
        var streamReference = await module.InvokeAsync<IJSStreamReference>("takeRecording", cancellationToken, button).ConfigureAwait(false);
        await using var disposableReference = streamReference.ConfigureAwait(false);
        await using var recordingStream = await streamReference.OpenReadStreamAsync(maximumBytes, cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        await recordingStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    /// <inheritdoc/>
    public async ValueTask DetachAsync(ElementReference button)
    {
        if (ModuleTask is null)
            return;

        var module = await ModuleTask.ConfigureAwait(false);
        await module.InvokeVoidAsync("detach", button).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (ModuleTask is null)
            return;

        try
        {
            var module = await ModuleTask.ConfigureAwait(false);
            await module.DisposeAsync().ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private Task<IJSObjectReference> GetModuleAsync()
        => ModuleTask ??= JsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();
}
