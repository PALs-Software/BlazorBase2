using BlazorBase.Speech.Components.PushToTalk;
using BlazorBase.Speech.Interop;
using BlazorBase.Speech.Models;
using BlazorBase.Speech.Services;
using BlazorBase.Speech.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using NSubstitute;
using Xunit;

namespace BlazorBase.Speech.Test.Components;

public sealed class PushToTalkButtonTests : BunitContext
{
    private readonly ISpeechRecorderInterop RecorderInterop = Substitute.For<ISpeechRecorderInterop>();
    private readonly CultureScope Culture = new("en");

    public PushToTalkButtonTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
        Services.AddLocalization();
        Services.AddSingleton(RecorderInterop);
        Services.AddScoped<SpeechMessages>();
    }

    protected override void Dispose(bool disposing)
    {
        Culture.Dispose();
        base.Dispose(disposing);
    }

    [Fact]
    public void FirstRender_AttachesTheRecorderWithTheLimits()
    {
        Render<PushToTalkButton>(parameters => parameters
            .Add(button => button.MaximumDurationSeconds, 30)
            .Add(button => button.MinimumDurationMilliseconds, 250));

        RecorderInterop.Received(1).AttachAsync(
            Arg.Any<ElementReference>(),
            Arg.Any<DotNetObjectReference<PushToTalkButton>>(),
            new SpeechRecorderSettings(30000, 250));
    }

    [Fact]
    public void Idle_ShowsTheHoldHint()
    {
        var cut = Render<PushToTalkButton>();

        var button = cut.Find("button.push-to-talk-button");
        Assert.Equal("false", button.GetAttribute("aria-pressed"));
        Assert.Equal("Hold to talk", button.GetAttribute("aria-label"));
        Assert.Equal("Hold to talk", cut.Find(".push-to-talk-caption").TextContent);
    }

    [Fact]
    public async Task RecordingStarted_SwitchesStateAndRaisesTheCallback()
    {
        var started = false;
        var cut = Render<PushToTalkButton>(parameters => parameters
            .Add(button => button.OnRecordingStarted, () => started = true));

        await cut.InvokeAsync(() => cut.Instance.NotifyRecordingStarted());

        var button = cut.Find("button.push-to-talk-button");
        Assert.True(started);
        Assert.Equal("true", button.GetAttribute("aria-pressed"));
        Assert.Contains("push-to-talk-recording", button.ClassList);
        Assert.Equal("Listening … release to send", cut.Find(".push-to-talk-caption").TextContent);
    }

    [Fact]
    public async Task RecordingCompleted_HandsOverTheRecording()
    {
        byte[] wav = [82, 73, 70, 70];
        RecorderInterop
            .TakeRecordingAsync(Arg.Any<ElementReference>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(wav));
        SpeechRecording? received = null;
        var cut = Render<PushToTalkButton>(parameters => parameters
            .Add(button => button.OnRecorded, recording => received = recording));

        await cut.InvokeAsync(() => cut.Instance.NotifyRecordingStarted());
        await cut.InvokeAsync(() => cut.Instance.NotifyRecordingCompleted(1500));

        Assert.NotNull(received);
        Assert.Same(wav, received.Wav);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), received.Duration);
        Assert.Equal("false", cut.Find("button.push-to-talk-button").GetAttribute("aria-pressed"));
    }

    [Fact]
    public async Task RecordingCompleted_AllowsSixtySecondsOfAudioByDefault()
    {
        var cut = Render<PushToTalkButton>();

        await cut.InvokeAsync(() => cut.Instance.NotifyRecordingCompleted(60000));

        await RecorderInterop.Received(1).TakeRecordingAsync(
            Arg.Any<ElementReference>(),
            Arg.Is<long>(limit => limit >= 60L * 16000 * 2 + 44),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordingFailed_ShowsTheLocalizedReason()
    {
        var cut = Render<PushToTalkButton>();

        await cut.InvokeAsync(() => cut.Instance.NotifyRecordingFailed("PermissionDenied"));

        Assert.Contains("Microphone access was denied", cut.Find("[role=alert]").TextContent);
    }

    [Fact]
    public async Task RecordingFailed_UnknownCode_FallsBackToTheGenericMessage()
    {
        var cut = Render<PushToTalkButton>();

        await cut.InvokeAsync(() => cut.Instance.NotifyRecordingFailed("SomethingNew"));

        Assert.Contains("The recording failed", cut.Find("[role=alert]").TextContent);
    }

    [Fact]
    public async Task NewRecording_ClearsAPreviousError()
    {
        var cut = Render<PushToTalkButton>();
        await cut.InvokeAsync(() => cut.Instance.NotifyRecordingFailed("NoMicrophone"));

        await cut.InvokeAsync(() => cut.Instance.NotifyRecordingStarted());

        Assert.Empty(cut.FindAll("[role=alert]"));
    }

    [Fact]
    public void Disabled_DisablesTheButton()
    {
        var cut = Render<PushToTalkButton>(parameters => parameters.Add(button => button.Disabled, true));

        Assert.True(cut.Find("button.push-to-talk-button").HasAttribute("disabled"));
    }

    [Fact]
    public void GermanCulture_UsesGermanTexts()
    {
        using var culture = new CultureScope("de");

        var cut = Render<PushToTalkButton>();

        Assert.Equal("Gedrückt halten zum Sprechen", cut.Find(".push-to-talk-caption").TextContent);
    }
}
