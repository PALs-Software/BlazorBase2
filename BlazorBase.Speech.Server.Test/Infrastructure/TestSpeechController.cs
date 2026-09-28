using BlazorBase.Speech.Server.Configuration;
using BlazorBase.Speech.Server.Controllers;
using BlazorBase.Speech.Server.Localization;
using BlazorBase.Speech.Server.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace BlazorBase.Speech.Server.Test.Infrastructure;

/// <summary>The thin concrete controller a host would write.</summary>
public sealed class TestSpeechController(
    ISpeechApiClient speechApiClient,
    SpeechRequestGate requestGate,
    IOptions<SpeechServerOptions> options,
    IStringLocalizer<SpeechServerText> localizer)
    : SpeechControllerBase(speechApiClient, requestGate, options, localizer);
