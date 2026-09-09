using System.Text;

namespace BlazorBase.User.SecurityHarness.IdentityProvider;

/// <summary>Unpadded base64url encoding, the segment encoding every compact JWS uses.</summary>
internal static class Base64UrlEncoding
{
    public static string Encode(string value) => Encode(Encoding.UTF8.GetBytes(value));

    public static string Encode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// The reverse of <see cref="Encode(byte[])"/> - restoring standard base64 padding and alphabet
    /// before decoding, so a probe can turn a JWS segment it sent back into readable evidence.
    /// </summary>
    public static byte[] Decode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - (padded.Length % 4)) % 4);

        return Convert.FromBase64String(padded);
    }
}
