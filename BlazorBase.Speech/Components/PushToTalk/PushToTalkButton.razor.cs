using BlazorBase.Speech.Contracts;
using BlazorBase.Speech.Interop;
using BlazorBase.Speech.Models;
using BlazorBase.Speech.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBase.Speech.Components.PushToTalk;

/// <summary>
/// A microphone button that records while it is held - by pointer, touch, or Space/Enter while focused -
/// and hands the recording over as 16 kHz mono WAV when it is released.
/// </summary>
/// <remarks>
/// The microphone is opened on every press and released on every release. Keeping it open would save
/// the fraction of a second it takes to open, but Safari on iOS routes all audio to the earpiece while
/// a microphone is open, which would make a spoken answer nearly inaudible. Each press also primes
/// audio playback, because a press is the user gesture browsers require before audio may play.
/// </remarks>
public partial class PushToTalkButton(
    ISpeechRecorderInterop recorderInterop,
    SpeechMessages speechMessages,
    IStringLocalizer<PushToTalkButton> localizer) : ComponentBase, IAsyncDisposable
{
    #region Injects
    private readonly ISpeechRecorderInterop RecorderInterop = recorderInterop;
    private readonly SpeechMessages SpeechMessages = speechMessages;
    private readonly IStringLocalizer<PushToTalkButton> Localizer = localizer;
    #endregion

    /// <summary>Prevents recording, for example while the previous recording is still being processed.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Recording stops by itself after this many seconds. Defaults to 60.</summary>
    [Parameter]
    public int MaximumDurationSeconds { get; set; } = 60;

    /// <summary>Presses shorter than this are treated as accidental taps and discarded. Defaults to 300 ms.</summary>
    [Parameter]
    public int MinimumDurationMilliseconds { get; set; } = 300;

    /// <summary>Shows the hint next to the button. The hint stays available to screen readers either way.</summary>
    [Parameter]
    public bool ShowCaption { get; set; } = true;

    /// <summary>Additional CSS classes for the wrapper.</summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>Raised as soon as the microphone records - the moment to stop anything that is being read aloud.</summary>
    [Parameter]
    public EventCallback OnRecordingStarted { get; set; }

    /// <summary>Raised with the finished recording when the button is released.</summary>
    [Parameter]
    public EventCallback<SpeechRecording> OnRecorded { get; set; }

    private ElementReference ButtonElement;

    private DotNetObjectReference<PushToTalkButton>? CallbackReference;

    private PushToTalkState State { get; set; } = PushToTalkState.Idle;

    private string? ErrorMessage { get; set; }

    private string CaptionId { get; } = $"push-to-talk-caption-{Guid.NewGuid():N}";

    private bool IsRecording => State == PushToTalkState.Recording;

    private string StateClass => IsRecording ? "push-to-talk-recording" : string.Empty;

    private string ButtonLabel => IsRecording ? Localizer["RecordingLabel"] : Localizer["IdleLabel"];

    private string Caption => IsRecording ? Localizer["RecordingCaption"] : Localizer["IdleCaption"];

    private static readonly Icon IdleIcon = new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size24.Mic();

    private static readonly Icon RecordingIcon = new Microsoft.FluentUI.AspNetCore.Components.Icons.Filled.Size24.Mic();

    private Icon ButtonIcon => IsRecording ? RecordingIcon : IdleIcon;

    private long MaximumRecordingBytes
        => (long)MaximumDurationSeconds * SpeechAudio.RecordingSampleRate * SpeechAudio.RecordingBytesPerSample
           + SpeechAudio.WavHeaderLength + SpeechAudio.RecordingSampleRate;

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        CallbackReference = DotNetObjectReference.Create(this);
        var settings = new SpeechRecorderSettings(MaximumDurationSeconds * 1000, MinimumDurationMilliseconds);
        await RecorderInterop.AttachAsync(ButtonElement, CallbackReference, settings);
    }

    /// <summary>Called by the recorder module once the microphone is open.</summary>
    [JSInvokable]
    public async Task NotifyRecordingStarted()
    {
        ErrorMessage = null;
        State = PushToTalkState.Recording;
        StateHasChanged();
        await OnRecordingStarted.InvokeAsync();
    }

    /// <summary>Called by the recorder module when a recording long enough to keep has finished.</summary>
    [JSInvokable]
    public async Task NotifyRecordingCompleted(int durationMilliseconds)
    {
        State = PushToTalkState.Idle;
        StateHasChanged();

        byte[] wav;

        try
        {
            wav = await RecorderInterop.TakeRecordingAsync(ButtonElement, MaximumRecordingBytes);
        }
        catch (JSException)
        {
            ShowError(SpeechRecorderError.Failed);
            return;
        }

        await OnRecorded.InvokeAsync(new SpeechRecording(wav, TimeSpan.FromMilliseconds(durationMilliseconds)));
    }

    /// <summary>Called by the recorder module when a press was too short to be meant.</summary>
    [JSInvokable]
    public Task NotifyRecordingDiscarded()
    {
        State = PushToTalkState.Idle;
        StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>Called by the recorder module when the microphone could not be used.</summary>
    [JSInvokable]
    public Task NotifyRecordingFailed(string error)
    {
        var recorderError = Enum.TryParse<SpeechRecorderError>(error, out var parsed) ? parsed : SpeechRecorderError.Failed;
        ShowError(recorderError);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await RecorderInterop.DetachAsync(ButtonElement);
        }
        catch (JSDisconnectedException)
        {
        }

        await RecorderInterop.DisposeAsync();
        CallbackReference?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ShowError(SpeechRecorderError error)
    {
        State = PushToTalkState.Idle;
        ErrorMessage = SpeechMessages.Describe(error);
        StateHasChanged();
    }
}
