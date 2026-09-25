using System.Globalization;
using System.Security.Claims;
using System.Text;
using BlazorBase.Speech.Contracts;
using BlazorBase.Speech.Models;
using BlazorBase.Speech.Server.Configuration;
using BlazorBase.Speech.Server.Localization;
using BlazorBase.Speech.Server.Services;
using BlazorBase.Speech.Server.Test.Infrastructure;
using BlazorBase.Speech.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace BlazorBase.Speech.Server.Test.Controllers;

public sealed class SpeechControllerBaseTests : IDisposable
{
    private static readonly byte[] ValidWav = [.. "RIFF"u8, 36, 0, 0, 0, .. "WAVEfmt "u8, 16, 0, 0, 0];

    private readonly ISpeechApiClient SpeechApiClient = Substitute.For<ISpeechApiClient>();
    private readonly SpeechServerOptions Options = new() { BaseUrl = "http://speech-api:8080" };
    private readonly CultureInfo PreviousCulture = CultureInfo.CurrentUICulture;

    public SpeechControllerBaseTests() => CultureInfo.CurrentUICulture = new CultureInfo("en");

    public void Dispose() => CultureInfo.CurrentUICulture = PreviousCulture;

    private TestSpeechController CreateController(SpeechRequestGate? gate = null, string userId = "user-1")
    {
        var options = Microsoft.Extensions.Options.Options.Create(Options);
        var controller = new TestSpeechController(
            SpeechApiClient,
            gate ?? new SpeechRequestGate(options),
            options,
            Localizers.For<SpeechServerText>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test"))
            }
        };

        return controller;
    }

    private static FormFile CreateFile(byte[] content)
        => new(new MemoryStream(content), 0, content.Length, "file", "recording.wav");

    private static ProblemDetails AssertProblem(IActionResult? result, int expectedStatus)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        return Assert.IsType<ProblemDetails>(objectResult.Value);
    }

    [Fact]
    public void GetAvailability_ReflectsTheConfiguration()
    {
        Assert.True(CreateController().GetAvailability().Value!.IsAvailable);

        Options.BaseUrl = null;

        Assert.False(CreateController().GetAvailability().Value!.IsAvailable);
    }

    [Fact]
    public async Task NotConfigured_BothRoutesAnswer503()
    {
        Options.BaseUrl = " ";
        var controller = CreateController();

        var transcription = await controller.Transcribe(CreateFile(ValidWav), "de", CancellationToken.None);
        var synthesis = await controller.Synthesize(new SpeechSynthesisRequest("Hallo", "de"), CancellationToken.None);

        AssertProblem(transcription.Result, StatusCodes.Status503ServiceUnavailable);
        AssertProblem(synthesis, StatusCodes.Status503ServiceUnavailable);
        await SpeechApiClient.DidNotReceiveWithAnyArgs().TranscribeAsync(default!, default, default);
    }

    [Fact]
    public async Task Transcribe_ForwardsTheRecordingAndReturnsTheText()
    {
        byte[]? forwarded = null;
        SpeechApiClient
            .TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                using var buffer = new MemoryStream();
                call.ArgAt<Stream>(0).CopyTo(buffer);
                forwarded = buffer.ToArray();
                return "Hallo Welt";
            });

        var result = await CreateController().Transcribe(CreateFile(ValidWav), "de", CancellationToken.None);

        Assert.Equal("Hallo Welt", result.Value!.Text);
        Assert.Equal(ValidWav, forwarded);
    }

    [Theory]
    [InlineData("de", "de")]
    [InlineData("DE", "de")]
    [InlineData("de-DE", "de")]
    [InlineData("en_US", "en")]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("d3", null)]
    [InlineData("deutsch", null)]
    public async Task Transcribe_NormalizesTheLanguage(string? language, string? expected)
    {
        SpeechApiClient.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns("x");

        await CreateController().Transcribe(CreateFile(ValidWav), language, CancellationToken.None);

        await SpeechApiClient.Received(1).TranscribeAsync(Arg.Any<Stream>(), expected, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transcribe_MissingOrEmptyRecording_Is400()
    {
        var controller = CreateController();

        AssertProblem((await controller.Transcribe(null, "de", CancellationToken.None)).Result, StatusCodes.Status400BadRequest);
        AssertProblem((await controller.Transcribe(CreateFile([]), "de", CancellationToken.None)).Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Transcribe_TooLarge_Is413WithALocalizedDetail()
    {
        Options.MaxUploadBytes = ValidWav.Length - 1;

        var result = await CreateController().Transcribe(CreateFile(ValidWav), "de", CancellationToken.None);

        var problem = AssertProblem(result.Result, StatusCodes.Status413PayloadTooLarge);
        Assert.Equal("The recording is too long.", problem.Detail);
    }

    [Fact]
    public async Task Transcribe_NotAWav_Is415()
    {
        var result = await CreateController().Transcribe(CreateFile(Encoding.ASCII.GetBytes("ID3 this is an mp3 file")), "de", CancellationToken.None);

        AssertProblem(result.Result, StatusCodes.Status415UnsupportedMediaType);
        await SpeechApiClient.DidNotReceiveWithAnyArgs().TranscribeAsync(default!, default, default);
    }

    [Fact]
    public async Task Transcribe_UserAtTheLimit_Is429_OtherUsersAreNotAffected()
    {
        Options.MaxConcurrentRequestsPerUser = 1;
        var gate = new SpeechRequestGate(Microsoft.Extensions.Options.Options.Create(Options));
        SpeechApiClient.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns("x");
        using var busy = gate.TryEnter("user-1");

        var blocked = await CreateController(gate, "user-1").Transcribe(CreateFile(ValidWav), "de", CancellationToken.None);
        var other = await CreateController(gate, "user-2").Transcribe(CreateFile(ValidWav), "de", CancellationToken.None);

        AssertProblem(blocked.Result, StatusCodes.Status429TooManyRequests);
        Assert.Equal("x", other.Value!.Text);
    }

    [Fact]
    public async Task Transcribe_ReleasesTheSlotAfterwards()
    {
        Options.MaxConcurrentRequestsPerUser = 1;
        var gate = new SpeechRequestGate(Microsoft.Extensions.Options.Options.Create(Options));
        SpeechApiClient.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns("x");
        var controller = CreateController(gate);

        await controller.Transcribe(CreateFile(ValidWav), "de", CancellationToken.None);
        var second = await controller.Transcribe(CreateFile(ValidWav), "de", CancellationToken.None);

        Assert.Equal("x", second.Value!.Text);
    }

    [Theory]
    [InlineData(SpeechFailureKind.Busy, StatusCodes.Status503ServiceUnavailable)]
    [InlineData(SpeechFailureKind.Timeout, StatusCodes.Status504GatewayTimeout)]
    [InlineData(SpeechFailureKind.Rejected, StatusCodes.Status400BadRequest)]
    [InlineData(SpeechFailureKind.Unavailable, StatusCodes.Status502BadGateway)]
    public async Task ServiceFailures_MapToStatusCodes(SpeechFailureKind kind, int expectedStatus)
    {
        SpeechApiClient
            .TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new SpeechRequestException(kind, "failed"));
        SpeechApiClient
            .SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new SpeechRequestException(kind, "failed"));
        var controller = CreateController();

        var transcription = await controller.Transcribe(CreateFile(ValidWav), "de", CancellationToken.None);
        var synthesis = await controller.Synthesize(new SpeechSynthesisRequest("Hallo", "de"), CancellationToken.None);

        AssertProblem(transcription.Result, expectedStatus);
        AssertProblem(synthesis, expectedStatus);
    }

    [Fact]
    public async Task Synthesize_ReturnsWavAudio()
    {
        SpeechApiClient.SynthesizeAsync("Guten Tag", "de", Arg.Any<CancellationToken>()).Returns([1, 2, 3]);

        var result = await CreateController().Synthesize(new SpeechSynthesisRequest("Guten Tag", "de-DE"), CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("audio/wav", file.ContentType);
        Assert.Equal([1, 2, 3], file.FileContents);
    }

    [Fact]
    public async Task Synthesize_EmptyOrTooLongText_Is400()
    {
        Options.MaxTextCharacters = 5;
        var controller = CreateController();

        AssertProblem(await controller.Synthesize(new SpeechSynthesisRequest("  ", "de"), CancellationToken.None), StatusCodes.Status400BadRequest);
        AssertProblem(await controller.Synthesize(new SpeechSynthesisRequest("zu lang", "de"), CancellationToken.None), StatusCodes.Status400BadRequest);
        await SpeechApiClient.DidNotReceiveWithAnyArgs().SynthesizeAsync(default!, default, default);
    }

    [Fact]
    public async Task ProblemDetails_FollowTheUiCulture()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("de");

        var result = await CreateController().Synthesize(new SpeechSynthesisRequest("", "de"), CancellationToken.None);

        Assert.Equal("Es gibt keinen Text zum Vorlesen.", AssertProblem(result, StatusCodes.Status400BadRequest).Detail);
    }
}
