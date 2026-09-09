using System.Security.Cryptography;

namespace BlazorBase.User.SecurityHarness.Probes.TwoFactor;

/// <summary>
/// A from-scratch RFC 6238 (TOTP) implementation, computed directly against the base32 authenticator
/// key the framework hands out through <c>api/auth/manage/2fa</c>'s <c>AuthenticatorUri</c> - the
/// same algorithm ASP.NET Core Identity's <c>AuthenticatorTokenProvider</c> runs internally (30-second
/// step, HMAC-SHA1, 6 digits, no modifier - the standard shape any authenticator app produces),
/// reimplemented here rather than reached into via reflection, since that internal type is not part
/// of the framework's public surface.
/// </summary>
internal static class Totp
{
    private const int StepSeconds = 30;
    private const int Digits = 6;
    private const long Modulus = 1_000_000;

    /// <summary>The 6-digit code for <paramref name="base32Secret"/> at <paramref name="timestamp"/>, using the real wall clock - Identity's own TOTP verification is not wired to any injectable clock.</summary>
    public static string ComputeCode(string base32Secret, DateTimeOffset timestamp)
    {
        var key = Base32Decode(base32Secret);
        var timestepNumber = (ulong)(timestamp.ToUnixTimeSeconds() / StepSeconds);

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

        var code = (uint)(binaryCode % Modulus);
        return code.ToString().PadLeft(Digits, '0');
    }

    /// <summary>RFC 4648 base32 decoding (no padding required) of the key format <c>UserManager.GetAuthenticatorKeyAsync</c> returns.</summary>
    public static byte[] Base32Decode(string base32)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var cleaned = base32.Trim().TrimEnd('=').ToUpperInvariant();

        var output = new List<byte>(cleaned.Length * 5 / 8);
        var bitBuffer = 0;
        var bitsInBuffer = 0;

        foreach (var character in cleaned)
        {
            var value = alphabet.IndexOf(character);

            if (value < 0)
                continue;

            bitBuffer = (bitBuffer << 5) | value;
            bitsInBuffer += 5;

            if (bitsInBuffer >= 8)
            {
                bitsInBuffer -= 8;
                output.Add((byte)((bitBuffer >> bitsInBuffer) & 0xff));
            }
        }

        return [.. output];
    }
}
