namespace BlazorBase.User.Server.AccessTokens.Generation;

/// <summary>
/// Generates, hashes, and verifies access-token secrets.
/// </summary>
public interface IAccessTokenSecretGenerator
{
    /// <summary>Creates a new random secret together with its hash and lookup prefix.</summary>
    GeneratedSecret Generate();

    /// <summary>Returns the SHA-512 base64 hash of <paramref name="secret"/>.</summary>
    string Hash(string secret);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="presentedSecret"/> produces the same
    /// hash as <paramref name="storedHash"/>, using a constant-time comparison.
    /// </summary>
    bool MatchesHash(string presentedSecret, string storedHash);

    /// <summary>Extracts the non-secret prefix from a raw secret string.</summary>
    string ExtractPrefix(string secret);
}
