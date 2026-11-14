using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace BlazorBase.User.Server.Invitations;

/// <summary>
/// Creates invitation tokens (32 random bytes, Base64Url) and their SHA-256 hashes, and compares a
/// presented token with a stored hash in constant time. 32 random bytes resist brute force on their own
/// entropy, so the hash needs no salt.
/// </summary>
public static class UserInvitationTokenGenerator
{
    private const int TokenByteLength = 32;

    /// <summary>Creates a token: 43 Base64Url characters of 32 random bytes.</summary>
    public static string CreateToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenByteLength));

    /// <summary>Returns the Base64Url SHA-256 hash of <paramref name="token"/>.</summary>
    public static string Hash(string token) => Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Compares the hash of <paramref name="token"/> with <paramref name="storedHash"/> in constant time.</summary>
    public static bool MatchesHash(string token, string storedHash) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(token)), Encoding.ASCII.GetBytes(storedHash));
}
