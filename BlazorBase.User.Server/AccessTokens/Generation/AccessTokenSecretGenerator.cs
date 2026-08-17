using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.AccessTokens.Generation;

/// <summary>
/// Generates access-token secrets carrying the configured
/// <see cref="AccessTokenSettings.SecretPrefix"/>, hashed with SHA-512 for storage.
/// </summary>
/// <remarks>
/// The lookup key stored as <see cref="Entities.AccessToken.Prefix"/> is a fixed-length substring
/// of the full secret - the configured prefix plus the first <see cref="PrefixBodyLength"/>
/// characters of the Base64Url-encoded body - taken by position (<see cref="ExtractPrefix"/>),
/// never by searching for a delimiter. That matters: the Base64Url body can itself contain the
/// underscore that conventionally ends a prefix (Base64Url maps <c>/</c> to <c>_</c>), so any
/// implementation that relocated the boundary with <c>Split</c>/<c>IndexOf</c> would sometimes cut
/// in the wrong place. Because there is exactly one variable field after the fixed prefix, no
/// separator is needed at all.
///
/// No per-token salt is stored, and none is needed: the secret is 32+ cryptographically random
/// bytes, so a stolen hash resists brute force on its own entropy - a salt only defends secrets
/// weak enough to appear in a precomputed table, which these never are.
/// </remarks>
public class AccessTokenSecretGenerator(IOptions<AccessTokenSettings> options) : IAccessTokenSecretGenerator
{
    #region Injects
    private readonly AccessTokenSettings Settings = options.Value;
    #endregion

    private const int PrefixBodyLength = 8;
    private const int MinimumSecretByteLength = 32;

    public GeneratedSecret Generate()
    {
        var byteLength = Math.Max(MinimumSecretByteLength, Settings.SecretByteLength);
        var randomBytes = RandomNumberGenerator.GetBytes(byteLength);

        var secret = Settings.SecretPrefix + Convert.ToBase64String(randomBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return new GeneratedSecret(ExtractPrefix(secret), secret, Hash(secret));
    }

    public string Hash(string secret)
    {
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var hashBytes = SHA512.HashData(secretBytes);
        return Convert.ToBase64String(hashBytes);
    }

    public bool MatchesHash(string presentedSecret, string storedHash)
    {
        var presentedHash = Hash(presentedSecret);
        var presentedHashBytes = Encoding.UTF8.GetBytes(presentedHash);
        var storedHashBytes = Encoding.UTF8.GetBytes(storedHash);
        return CryptographicOperations.FixedTimeEquals(presentedHashBytes, storedHashBytes);
    }

    public string ExtractPrefix(string secret)
    {
        var prefixLength = Settings.SecretPrefix.Length + PrefixBodyLength;

        if (secret.Length < prefixLength)
            return secret;

        return secret[..prefixLength];
    }
}
