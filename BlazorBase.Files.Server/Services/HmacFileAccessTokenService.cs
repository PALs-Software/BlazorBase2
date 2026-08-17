using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace BlazorBase.Files.Server.Services;

/// <summary>
/// HMAC-SHA256 implementation of <see cref="IFileAccessTokenService"/>. Tokens encode
/// <c>{fileId}|{expiryUnixSeconds}</c>, HMAC-signed with a configurable key and base64url-encoded.
/// Signature and expiry are both validated on every call to <see cref="ValidateToken"/>.
/// </summary>
public class HmacFileAccessTokenService(IOptions<FileAccessTokenOptions> tokenOptions) : IFileAccessTokenService
{
    #region Injects
    private readonly FileAccessTokenOptions TokenOptions = tokenOptions.Value;
    #endregion

    /// <inheritdoc/>
    public string IssueToken(Guid fileId)
    {
        var signingKey = ResolveSigningKey();
        var expiry = DateTimeOffset.UtcNow.AddSeconds(TokenOptions.TokenLifetimeSeconds).ToUnixTimeSeconds();
        var payload = BuildPayload(fileId, expiry);
        var signature = ComputeHmac(signingKey, payload);

        var tokenBytes = Encoding.UTF8.GetBytes($"{payload}.{Convert.ToBase64String(signature)}");
        return Base64UrlEncode(tokenBytes);
    }

    /// <inheritdoc/>
    public bool ValidateToken(Guid fileId, string token)
    {
        try
        {
            var tokenBytes = Base64UrlDecode(token);
            var tokenString = Encoding.UTF8.GetString(tokenBytes);

            var lastDot = tokenString.LastIndexOf('.');
            if (lastDot < 0)
                return false;

            var payload = tokenString[..lastDot];
            var signatureBase64 = tokenString[(lastDot + 1)..];

            var parts = payload.Split('|');
            if (parts.Length != 2)
                return false;

            if (!Guid.TryParse(parts[0], out var tokenFileId) || tokenFileId != fileId)
                return false;

            if (!long.TryParse(parts[1], out var expiryUnixSeconds))
                return false;

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiryUnixSeconds)
                return false;

            var signingKey = ResolveSigningKey();
            var expectedSignature = ComputeHmac(signingKey, payload);
            var actualSignature = Convert.FromBase64String(signatureBase64);

            return CryptographicOperations.FixedTimeEquals(expectedSignature, actualSignature);
        }
        catch
        {
            return false;
        }
    }

    private byte[] ResolveSigningKey()
    {
        if (string.IsNullOrWhiteSpace(TokenOptions.SigningKeyBase64))
            throw new InvalidOperationException("FileAccessTokenOptions.SigningKeyBase64 must be configured with a base64-encoded random key of at least 32 bytes.");

        var keyBytes = Convert.FromBase64String(TokenOptions.SigningKeyBase64);

        if (keyBytes.Length < 32)
            throw new InvalidOperationException("FileAccessTokenOptions.SigningKeyBase64 must encode at least 32 bytes (256 bits).");

        return keyBytes;
    }

    private static string BuildPayload(Guid fileId, long expiryUnixSeconds) =>
        $"{fileId:D}|{expiryUnixSeconds}";

    private static byte[] ComputeHmac(byte[] key, string payload)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    }

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input
            .Replace('-', '+')
            .Replace('_', '/');

        var paddingNeeded = (4 - padded.Length % 4) % 4;
        padded += new string('=', paddingNeeded);

        return Convert.FromBase64String(padded);
    }
}
