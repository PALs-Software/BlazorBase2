using BlazorBase.Speech.Models;
using Microsoft.Extensions.Localization;

namespace BlazorBase.Speech.Services;

/// <summary>
/// Localized, user-facing explanations for recorder errors and failed speech requests, so every host
/// shows the same wording without carrying its own copy.
/// </summary>
public class SpeechMessages(IStringLocalizer<SpeechMessages> localizer)
{
    #region Injects
    private readonly IStringLocalizer<SpeechMessages> Localizer = localizer;
    #endregion

    /// <summary>Explains why the browser could not record.</summary>
    public string Describe(SpeechRecorderError error) => Localizer[$"Recorder{error}"];

    /// <summary>Explains why a transcription or synthesis request failed.</summary>
    public string Describe(SpeechFailureKind kind) => Localizer[$"Request{kind}"];
}
