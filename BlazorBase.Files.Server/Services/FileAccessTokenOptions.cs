namespace BlazorBase.Files.Server.Services;

/// <summary>
/// Options governing the signed file-access token issued by <see cref="HmacFileAccessTokenService"/>.
/// Configure via the <c>BlazorBaseFiles:TokenOptions</c> configuration section or via
/// <c>AddBlazorBaseFilesServer</c>.
/// </summary>
public class FileAccessTokenOptions
{
    /// <summary>
    /// HMAC-SHA256 signing key, base64-encoded. Must be at least 32 bytes (256 bits) of
    /// cryptographically random data. <strong>Never hardcode this value — inject it from a
    /// secrets manager or environment variable.</strong>
    /// </summary>
    public string SigningKeyBase64 { get; set; } = string.Empty;

    /// <summary>
    /// Token lifetime in seconds. Defaults to 300 (five minutes). The signed token is the sole
    /// access gate for <c>Download</c>/<c>Thumbnail</c>, so a short default limits how long a
    /// leaked link remains usable.
    /// </summary>
    public int TokenLifetimeSeconds { get; set; } = 300;
}
