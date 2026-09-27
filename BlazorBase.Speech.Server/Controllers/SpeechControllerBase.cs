using System.Security.Claims;
using System.Text.RegularExpressions;
using BlazorBase.Speech.Contracts;
using BlazorBase.Speech.Models;
using BlazorBase.Speech.Server.Configuration;
using BlazorBase.Speech.Server.Localization;
using BlazorBase.Speech.Server.Services;
using BlazorBase.Speech.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace BlazorBase.Speech.Server.Controllers;

/// <summary>
/// Authenticated proxy between the browser and the internal speech service, which has no
/// authentication of its own and must never be reachable from the browser directly. The host derives
/// a thin concrete class so ASP.NET Core discovers it through controller scanning.
/// </summary>
/// <remarks>
/// Recordings are checked for size and a RIFF/WAVE signature before they leave the host, and every
/// user is capped at <see cref="SpeechServerOptions.MaxConcurrentRequestsPerUser"/> requests in flight.
/// Neither recordings nor texts are logged or stored.
/// </remarks>
[ApiController]
[Route(SpeechRoutes.Base)]
[Authorize]
public abstract partial class SpeechControllerBase(
    ISpeechApiClient speechApiClient,
    SpeechRequestGate requestGate,
    IOptions<SpeechServerOptions> options,
    IStringLocalizer<SpeechServerText> localizer) : ControllerBase
{
    #region Injects
    private readonly ISpeechApiClient SpeechApiClient = speechApiClient;
    private readonly SpeechRequestGate RequestGate = requestGate;
    private readonly IOptions<SpeechServerOptions> Options = options;
    private readonly IStringLocalizer<SpeechServerText> Localizer = localizer;
    #endregion

    private const int WavSignatureLength = 12;

    /// <summary>Tells the client whether speech features can be offered.</summary>
    [HttpGet(SpeechRoutes.Availability)]
    public virtual ActionResult<SpeechAvailability> GetAvailability()
        => new SpeechAvailability(Options.Value.IsConfigured);

    /// <summary>Transcribes an uploaded WAV recording.</summary>
    [HttpPost(SpeechRoutes.Transcriptions)]
    public virtual async Task<ActionResult<TranscriptionResult>> Transcribe(
        [FromForm] IFormFile? file,
        [FromForm] string? language,
        CancellationToken cancellationToken)
    {
        if (!Options.Value.IsConfigured)
            return LocalizedProblem(StatusCodes.Status503ServiceUnavailable, "NotConfigured");

        if (file is null || file.Length == 0)
            return LocalizedProblem(StatusCodes.Status400BadRequest, "RecordingMissing");

        if (file.Length > Options.Value.MaxUploadBytes)
            return LocalizedProblem(StatusCodes.Status413PayloadTooLarge, "RecordingTooLarge");

        await using var recording = file.OpenReadStream();

        if (!await HasWavSignatureAsync(recording, cancellationToken))
            return LocalizedProblem(StatusCodes.Status415UnsupportedMediaType, "RecordingNotWav");

        using var lease = RequestGate.TryEnter(ResolveUserKey());

        if (lease is null)
            return LocalizedProblem(StatusCodes.Status429TooManyRequests, "TooManyRequests");

        try
        {
            recording.Seek(0, SeekOrigin.Begin);
            var text = await SpeechApiClient.TranscribeAsync(recording, NormalizeLanguage(language), cancellationToken);
            return new TranscriptionResult(text);
        }
        catch (SpeechRequestException exception)
        {
            return FailureProblem(exception.Kind);
        }
    }

    /// <summary>Lists the voices of the speech service, optionally only those of one language.</summary>
    [HttpGet(SpeechRoutes.Voices)]
    public virtual async Task<ActionResult<SpeechVoiceCatalog>> GetVoices([FromQuery] string? language, CancellationToken cancellationToken)
    {
        if (!Options.Value.IsConfigured)
            return LocalizedProblem(StatusCodes.Status503ServiceUnavailable, "NotConfigured");

        try
        {
            return await SpeechApiClient.GetVoicesAsync(NormalizeLanguage(language), cancellationToken);
        }
        catch (SpeechRequestException exception)
        {
            return FailureProblem(exception.Kind);
        }
    }

    /// <summary>Synthesizes WAV audio for plain text.</summary>
    [HttpPost(SpeechRoutes.Speech)]
    public virtual async Task<IActionResult> Synthesize([FromBody] SpeechSynthesisRequest request, CancellationToken cancellationToken)
    {
        if (!Options.Value.IsConfigured)
            return LocalizedProblem(StatusCodes.Status503ServiceUnavailable, "NotConfigured");

        if (string.IsNullOrWhiteSpace(request.Text))
            return LocalizedProblem(StatusCodes.Status400BadRequest, "TextMissing");

        if (request.Text.Length > Options.Value.MaxTextCharacters)
            return LocalizedProblem(StatusCodes.Status400BadRequest, "TextTooLong");

        if (!string.IsNullOrEmpty(request.Voice) && !VoiceName().IsMatch(request.Voice))
            return LocalizedProblem(StatusCodes.Status400BadRequest, "VoiceInvalid");

        using var lease = RequestGate.TryEnter(ResolveUserKey());

        if (lease is null)
            return LocalizedProblem(StatusCodes.Status429TooManyRequests, "TooManyRequests");

        try
        {
            var language = NormalizeLanguage(request.Language);
            var voice = string.IsNullOrEmpty(request.Voice)
                ? await ResolveDefaultVoiceAsync(language, cancellationToken)
                : request.Voice;
            var wav = await SpeechApiClient.SynthesizeAsync(request.Text, language, voice, cancellationToken);
            return File(wav, SpeechAudio.WavContentType);
        }
        catch (SpeechRequestException exception)
        {
            return FailureProblem(exception.Kind);
        }
    }

    /// <summary>
    /// The voice to read with when a request names none - for example a voice an administrator chose per
    /// language in the host's settings. Returning <see langword="null"/> (the default) leaves the choice to the
    /// speech service's own default for the language.
    /// </summary>
    /// <param name="language">The normalized two-letter language code, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    protected virtual Task<string?> ResolveDefaultVoiceAsync(string? language, CancellationToken cancellationToken)
        => Task.FromResult<string?>(null);

    /// <summary>
    /// Identifies the caller for the per-user request cap. Override when users are identified by a
    /// claim other than the name identifier.
    /// </summary>
    protected virtual string ResolveUserKey()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.Identity?.Name
           ?? HttpContext.Connection.Id;

    private ObjectResult FailureProblem(SpeechFailureKind kind) => kind switch
    {
        SpeechFailureKind.Busy => LocalizedProblem(StatusCodes.Status503ServiceUnavailable, "ServiceBusy"),
        SpeechFailureKind.Timeout => LocalizedProblem(StatusCodes.Status504GatewayTimeout, "ServiceTimeout"),
        SpeechFailureKind.Rejected => LocalizedProblem(StatusCodes.Status400BadRequest, "ServiceRejected"),
        _ => LocalizedProblem(StatusCodes.Status502BadGateway, "ServiceUnavailable")
    };

    private ObjectResult LocalizedProblem(int statusCode, string messageKey)
        => Problem(detail: Localizer[messageKey], statusCode: statusCode);

    private static async Task<bool> HasWavSignatureAsync(Stream recording, CancellationToken cancellationToken)
    {
        var signature = new byte[WavSignatureLength];
        var read = await recording.ReadAtLeastAsync(signature, WavSignatureLength, throwOnEndOfStream: false, cancellationToken);

        return read == WavSignatureLength
               && signature.AsSpan(0, 4).SequenceEqual("RIFF"u8)
               && signature.AsSpan(8, 4).SequenceEqual("WAVE"u8);
    }

    private static string? NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;

        var primaryTag = language.Trim().Split('-', '_')[0].ToLowerInvariant();
        return primaryTag.Length is 2 or 3 && primaryTag.All(char.IsAsciiLetterLower) ? primaryTag : null;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,100}$")]
    private static partial Regex VoiceName();
}
