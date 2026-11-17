using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace BlazorBase.User.Server.OAuth.Protocol;

/// <summary>
/// Creates and hashes the random values the authorization server hands out - client ids, client
/// secrets, authorization codes and refresh tokens - and compares a presented value with a stored hash
/// in constant time.
/// </summary>
/// <remarks>
/// SHA-256 without a salt is sufficient: every secret is at least 32 cryptographically random bytes, so
/// a stolen hash resists brute force on its own entropy; a salt only protects values weak enough to be
/// in a precomputed table.
/// </remarks>
public static class OAuthSecretGenerator
{
    /// <summary>The literal every client id starts with, so a client id is recognisable at a glance.</summary>
    public const string ClientIdPrefix = "bbc_";

    private const int ClientIdByteLength = 16;
    private const int SecretByteLength = 32;

    /// <summary>Creates a client id: <see cref="ClientIdPrefix"/> plus 22 Base64Url characters of 16 random bytes.</summary>
    public static string CreateClientId() => ClientIdPrefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(ClientIdByteLength));

    /// <summary>Creates a secret value: 43 Base64Url characters of 32 random bytes.</summary>
    public static string CreateSecret() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretByteLength));

    /// <summary>Returns the Base64Url SHA-256 hash of <paramref name="value"/>.</summary>
    public static string Hash(string value) => Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>Compares the hash of <paramref name="presentedValue"/> with <paramref name="storedHash"/> in constant time.</summary>
    public static bool MatchesHash(string presentedValue, string storedHash) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(presentedValue)), Encoding.ASCII.GetBytes(storedHash));
}
