using Microsoft.Extensions.Options;

namespace BlazorBase.Files.Server.Services;

/// <summary>
/// Validates <see cref="FileAccessTokenOptions"/> at application startup (registered alongside
/// <c>services.AddOptions&lt;FileAccessTokenOptions&gt;().ValidateOnStart()</c>) so a missing,
/// malformed, or too-short HMAC signing key fails fast during startup instead of surfacing as a
/// per-request failure the first time a token is issued or validated.
/// </summary>
public class FileAccessTokenOptionsValidator : IValidateOptions<FileAccessTokenOptions>
{
    private const int MinimumSigningKeyLengthBytes = 32;

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, FileAccessTokenOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SigningKeyBase64))
            return ValidateOptionsResult.Fail("FileAccessTokenOptions.SigningKeyBase64 must be configured with a base64-encoded random key of at least 32 bytes.");

        byte[] signingKeyBytes;

        try
        {
            signingKeyBytes = Convert.FromBase64String(options.SigningKeyBase64);
        }
        catch (FormatException)
        {
            return ValidateOptionsResult.Fail("FileAccessTokenOptions.SigningKeyBase64 must be a valid base64-encoded string.");
        }

        if (signingKeyBytes.Length < MinimumSigningKeyLengthBytes)
            return ValidateOptionsResult.Fail($"FileAccessTokenOptions.SigningKeyBase64 must encode at least {MinimumSigningKeyLengthBytes} bytes (256 bits).");

        return ValidateOptionsResult.Success;
    }
}
