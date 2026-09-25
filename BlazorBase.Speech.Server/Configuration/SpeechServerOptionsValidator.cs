using Microsoft.Extensions.Options;

namespace BlazorBase.Speech.Server.Configuration;

/// <summary>Rejects a malformed speech configuration at startup rather than on the first request.</summary>
public class SpeechServerOptionsValidator : IValidateOptions<SpeechServerOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, SpeechServerOptions options)
    {
        var failures = new List<string>();

        if (options.IsConfigured && !IsHttpUrl(options.BaseUrl!))
            failures.Add($"{SpeechServerOptions.SectionName}:{nameof(SpeechServerOptions.BaseUrl)} must be an absolute http or https URL.");

        if (options.TimeoutSeconds <= 0)
            failures.Add($"{SpeechServerOptions.SectionName}:{nameof(SpeechServerOptions.TimeoutSeconds)} must be positive.");

        if (options.MaxUploadBytes <= 0)
            failures.Add($"{SpeechServerOptions.SectionName}:{nameof(SpeechServerOptions.MaxUploadBytes)} must be positive.");

        if (options.MaxTextCharacters <= 0)
            failures.Add($"{SpeechServerOptions.SectionName}:{nameof(SpeechServerOptions.MaxTextCharacters)} must be positive.");

        if (options.MaxConcurrentRequestsPerUser <= 0)
            failures.Add($"{SpeechServerOptions.SectionName}:{nameof(SpeechServerOptions.MaxConcurrentRequestsPerUser)} must be positive.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsHttpUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
