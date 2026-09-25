using System.Text;
using BlazorBase.Speech.Models;
using BlazorBase.Speech.Services;
using BlazorBase.Speech.Test.Infrastructure;
using BlazorBase.Speech.Text;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace BlazorBase.Speech.Test.Services;

public sealed class SpeechNarratorTests
{
    private const string TwoSegments = "Erster Satz. " + "Zweiter Satz mit etwas mehr Inhalt, damit er zählt. ";

    private readonly List<string> Journal = [];
    private readonly ISpeechClient SpeechClient = Substitute.For<ISpeechClient>();
    private readonly RecordingAudioPlayer AudioPlayer;
    private readonly SpeechNarrator Narrator;

    public SpeechNarratorTests()
    {
        AudioPlayer = new RecordingAudioPlayer(Journal);
        SpeechClient
            .SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var text = call.ArgAt<string>(0);
                Journal.Add($"synthesize:{text}");
                return Task.FromResult(Encoding.UTF8.GetBytes(text));
            });

        Narrator = new SpeechNarrator(SpeechClient, AudioPlayer, new SpeechTextPreparer(Localizers.For<SpeechTextPreparer>()));
    }

    private static string LongText()
        => "Erster Satz. " + string.Join(' ', Enumerable.Range(1, 20).Select(index => $"Das ist Folgesatz Nummer {index} mit etwas Inhalt."));

    [Fact]
    public async Task SpeakAsync_PrefetchesTheNextSegmentBeforePlayingTheCurrentOne()
    {
        await Narrator.SpeakAsync(LongText(), "de");

        var synthesizeSecond = Journal.FindIndex(entry => entry.StartsWith("synthesize:Das ist Folgesatz Nummer 1 ", StringComparison.Ordinal));
        var playFirst = Journal.IndexOf("play:Erster Satz.");

        Assert.Equal("synthesize:Erster Satz.", Journal[0]);
        Assert.True(synthesizeSecond >= 0 && synthesizeSecond < playFirst);
        Assert.Equal(Journal.Count(entry => entry.StartsWith("synthesize:", StringComparison.Ordinal)), Journal.Count(entry => entry.StartsWith("play:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task SpeakAsync_PassesTheLanguageOnEveryRequest()
    {
        await Narrator.SpeakAsync(TwoSegments, "en");

        await SpeechClient.Received(2).SynthesizeAsync(Arg.Any<string>(), "en", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("This is the answer, and it is written in English for you.", "en")]
    [InlineData("Das ist die Antwort, und sie ist auf Deutsch für dich.", "de")]
    public async Task SpeakAsync_WithoutLanguage_ChoosesTheVoiceFromTheText(string text, string expectedLanguage)
    {
        await Narrator.SpeakAsync(text);

        await SpeechClient.Received().SynthesizeAsync(Arg.Any<string>(), expectedLanguage, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SpeakAsync_ExplicitLanguage_WinsOverTheGuess()
    {
        await Narrator.SpeakAsync("This is clearly an English answer for you.", "de");

        await SpeechClient.Received().SynthesizeAsync(Arg.Any<string>(), "de", Arg.Any<CancellationToken>());
        await SpeechClient.DidNotReceive().SynthesizeAsync(Arg.Any<string>(), "en", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SpeakAsync_NothingSpeakable_DoesNotCallTheService()
    {
        await Narrator.SpeakAsync("---", "de");
        await Narrator.SpeakAsync("   ", "de");

        await SpeechClient.DidNotReceive().SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        Assert.False(Narrator.IsSpeaking);
    }

    [Fact]
    public async Task StopAsync_EndsReadingAfterTheCurrentClip()
    {
        var firstClipPlaying = new TaskCompletionSource();
        var releaseFirstClip = new TaskCompletionSource<bool>();
        AudioPlayer.Playback = (_, _) =>
        {
            firstClipPlaying.TrySetResult();
            return releaseFirstClip.Task;
        };

        var speaking = Narrator.SpeakAsync(LongText(), "de");
        await firstClipPlaying.Task;
        Assert.True(Narrator.IsSpeaking);

        await Narrator.StopAsync();
        releaseFirstClip.SetResult(false);
        await speaking;

        Assert.False(Narrator.IsSpeaking);
        Assert.Equal(1, AudioPlayer.StopCount);
        Assert.Single(Journal, entry => entry.StartsWith("play:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SpeakingChanged_FiresOnStartAndEnd()
    {
        var changes = new List<bool>();
        Narrator.SpeakingChanged += () => changes.Add(Narrator.IsSpeaking);

        await Narrator.SpeakAsync(TwoSegments, "de");

        Assert.Equal([true, false], changes);
    }

    [Fact]
    public async Task SpeakAsync_SynthesisFailure_PropagatesAndResetsState()
    {
        SpeechClient
            .SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new SpeechRequestException(SpeechFailureKind.Unavailable, "down"));

        var exception = await Assert.ThrowsAsync<SpeechRequestException>(() => Narrator.SpeakAsync(TwoSegments, "de"));

        Assert.Equal(SpeechFailureKind.Unavailable, exception.Kind);
        Assert.False(Narrator.IsSpeaking);
    }

    [Fact]
    public async Task SpeakAsync_StopsWhateverWasReadBefore()
    {
        var firstClipPlaying = new TaskCompletionSource();
        var releaseFirstClip = new TaskCompletionSource<bool>();
        AudioPlayer.Playback = (_, _) =>
        {
            firstClipPlaying.TrySetResult();
            return releaseFirstClip.Task;
        };

        var first = Narrator.SpeakAsync(LongText(), "de");
        await firstClipPlaying.Task;

        AudioPlayer.Playback = (_, _) => Task.FromResult(true);
        var second = Narrator.SpeakAsync("Neue Antwort.", "de");
        releaseFirstClip.SetResult(false);
        await Task.WhenAll(first, second);

        Assert.Equal(1, AudioPlayer.StopCount);
        Assert.Equal("play:Neue Antwort.", Journal.Last(entry => entry.StartsWith("play:", StringComparison.Ordinal)));
        Assert.False(Narrator.IsSpeaking);
    }
}
