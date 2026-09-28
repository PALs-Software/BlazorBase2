using BlazorBase.Speech.Server.Configuration;
using BlazorBase.Speech.Server.Controllers;
using BlazorBase.Speech.Server.Localization;
using BlazorBase.Speech.Server.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace BlazorBase.Speech.Server.Test.Infrastructure;

/// <summary>A host controller that supplies its own default voice per language, as a settings page would.</summary>
public sealed class HostVoiceSpeechController(
    ISpeechApiClient speechApiClient,
    SpeechRequestGate requestGate,
    IOptions<SpeechServerOptions> options,
    IStringLocalizer<SpeechServerText> localizer)
    : SpeechControllerBase(speechApiClient, requestGate, options, localizer)
{
    public List<string?> RequestedLanguages { get; } = [];

    protected override Task<string?> ResolveDefaultVoiceAsync(string? language, CancellationToken cancellationToken)
    {
        RequestedLanguages.Add(language);
        return Task.FromResult<string?>(language == "de" ? "de_DE-kerstin-low" : null);
    }
}
