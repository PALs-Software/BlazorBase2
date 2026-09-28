using BlazorBase.Speech.Components.PushToTalk;
using BlazorBase.Speech.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBase.Speech.Interop;

/// <summary>Seam between <see cref="PushToTalkButton"/> and the browser-side recorder module.</summary>
public interface ISpeechRecorderInterop : IAsyncDisposable
{
    /// <summary>Wires press-and-hold handling (pointer and keyboard) onto the button element.</summary>
    ValueTask AttachAsync(ElementReference button, DotNetObjectReference<PushToTalkButton> callbackTarget, SpeechRecorderSettings settings);

    /// <summary>Hands over the finished recording as 16 kHz, 16-bit mono WAV.</summary>
    /// <param name="button">The button the recording belongs to.</param>
    /// <param name="maximumBytes">Upper bound for the transfer; larger recordings fail.</param>
    /// <param name="cancellationToken">Cancels the transfer.</param>
    ValueTask<byte[]> TakeRecordingAsync(ElementReference button, long maximumBytes, CancellationToken cancellationToken = default);

    /// <summary>Removes the handlers and releases the microphone.</summary>
    ValueTask DetachAsync(ElementReference button);
}
