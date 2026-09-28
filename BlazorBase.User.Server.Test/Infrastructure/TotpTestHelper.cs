using System.Security.Cryptography;
using System.Text;

namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>
/// Computes a TOTP code from a raw Base32 authenticator secret per RFC 6238, the same algorithm
/// <c>AuthenticatorTokenProvider</c> validates against - <c>UserManager</c> deliberately has no public
/// "generate the current code" API, since that is meant to happen on the authenticator app, not on
/// the server that already holds the secret.
/// </summary>
internal static class TotpTestHelper
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string GenerateCode(string base32Secret, DateTimeOffset timestamp)
    {
        var key = DecodeBase32(base32Secret);
        var timestepNumber = timestamp.ToUnixTimeSeconds() / 30;
        var timestepBytes = BitConverter.GetBytes(timestepNumber);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(timestepBytes);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(timestepBytes);
        var offset = hash[^1] & 0xf;

        var binaryCode = ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);

        return (binaryCode % 1_000_000).ToString("D6");
    }

    private static byte[] DecodeBase32(string base32Secret)
    {
        var trimmed = base32Secret.Replace(" ", string.Empty).TrimEnd('=').ToUpperInvariant();

        var bits = new StringBuilder(trimmed.Length * 5);
        foreach (var symbol in trimmed)
            bits.Append(Convert.ToString(Base32Alphabet.IndexOf(symbol), 2).PadLeft(5, '0'));

        var bytes = new List<byte>(bits.Length / 8);
        for (var i = 0; i + 8 <= bits.Length; i += 8)
            bytes.Add(Convert.ToByte(bits.ToString(i, 8), 2));

        return [.. bytes];
    }
}
