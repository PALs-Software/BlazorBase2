using System.Security.Cryptography;

namespace BlazorBase.Files.Server.Services;

/// <summary>
/// Shared SHA-256 hashing for <see cref="IFileStorage"/> implementations so every provider
/// produces identical digests for identical content.
/// </summary>
public static class FileContentHasher
{
    /// <summary>
    /// Computes the lower-case SHA-256 hex digest of <paramref name="content"/>. The stream position
    /// is reset to the beginning before reading and left at the end.
    /// </summary>
    /// <param name="content">Seekable, readable stream.</param>
    public static string ComputeSha256(Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);

        content.Seek(0, SeekOrigin.Begin);
        var hashBytes = SHA256.HashData(content);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
