using Microsoft.Extensions.Options;

namespace BlazorBase.Notifications.Server.Configuration;

/// <summary>
/// Rejects a malformed Web Push configuration at startup rather than on the first notification.
/// </summary>
public class WebPushOptionsValidator : IValidateOptions<WebPushOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, WebPushOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Subject))
            failures.Add($"{WebPushOptions.SectionName}:{nameof(WebPushOptions.Subject)} is required (a 'mailto:' address or an 'https://' URL).");
        else if (!IsMailtoOrHttpsUri(options.Subject))
            failures.Add($"{WebPushOptions.SectionName}:{nameof(WebPushOptions.Subject)} must be a 'mailto:' address or an 'https://' URL.");

        if (string.IsNullOrWhiteSpace(options.PublicKey))
            failures.Add($"{WebPushOptions.SectionName}:{nameof(WebPushOptions.PublicKey)} is required. Generate one with VapidKeyGenerator.Generate().");

        if (string.IsNullOrWhiteSpace(options.PrivateKey))
            failures.Add($"{WebPushOptions.SectionName}:{nameof(WebPushOptions.PrivateKey)} is required. Generate one with VapidKeyGenerator.Generate().");

        if (options.TimeToLiveSeconds <= 0)
            failures.Add($"{WebPushOptions.SectionName}:{nameof(WebPushOptions.TimeToLiveSeconds)} must be positive.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsMailtoOrHttpsUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeMailto || uri.Scheme == Uri.UriSchemeHttps);
}
