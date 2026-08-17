namespace BlazorBase.Files.Server.Services;

/// <summary>
/// Issues and validates short-lived signed tokens that authorize anonymous-looking HTTP GET
/// requests to file download and thumbnail endpoints. Tokens are needed because browser
/// <c>&lt;img&gt;</c> and <c>&lt;a&gt;</c> elements cannot attach a bearer header; the
/// signed token is passed as a query parameter instead.
/// </summary>
public interface IFileAccessTokenService
{
    /// <summary>
    /// Issues a short-lived HMAC-SHA256 signed token that grants access to the file identified
    /// by <paramref name="fileId"/>. The token encodes the file identifier and an expiry
    /// timestamp; it must be validated server-side before the file is served.
    /// </summary>
    /// <param name="fileId">The file for which access is granted.</param>
    /// <returns>A base64url-encoded signed token string.</returns>
    string IssueToken(Guid fileId);

    /// <summary>
    /// Validates a token previously issued by <see cref="IssueToken"/>. Returns
    /// <see langword="true"/> only when the signature is correct and the token has not expired.
    /// </summary>
    /// <param name="fileId">The file identifier the token must have been issued for.</param>
    /// <param name="token">The base64url-encoded token to validate.</param>
    bool ValidateToken(Guid fileId, string token);
}
