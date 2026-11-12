using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace BlazorBase.User.Server.OAuth.Protocol;

/// <summary>
/// Proof Key for Code Exchange (RFC 7636), restricted to the <c>S256</c> method: OAuth 2.1 makes PKCE
/// mandatory, and <c>plain</c> would hand the verifier to anyone who sees the authorization request.
/// </summary>
public static class OAuthPkce
{
    /// <summary>The only accepted <c>code_challenge_method</c>.</summary>
    public const string S256 = "S256";

    private const int MinimumLength = 43;
    private const int MaximumLength = 128;

    /// <summary>Returns <see langword="true"/> for 43 to 128 characters of the Base64Url alphabet.</summary>
    public static bool IsValidChallenge(string? challenge) =>
        HasValidLength(challenge) && challenge!.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    /// <summary>Returns <see langword="true"/> for 43 to 128 characters of the RFC 3986 unreserved set.</summary>
    public static bool IsValidVerifier(string? verifier) =>
        HasValidLength(verifier) && verifier!.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_' or '~');

    /// <summary>
    /// Returns <see langword="true"/> when <c>BASE64URL(SHA256(verifier))</c> equals
    /// <paramref name="challenge"/>, compared in constant time.
    /// </summary>
    public static bool Matches(string verifier, string challenge)
    {
        var computedChallenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(computedChallenge), Encoding.ASCII.GetBytes(challenge));
    }

    private static bool HasValidLength(string? value) => value is not null && value.Length is >= MinimumLength and <= MaximumLength;
}
