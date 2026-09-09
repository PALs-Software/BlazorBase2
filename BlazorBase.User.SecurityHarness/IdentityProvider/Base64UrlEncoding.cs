using System.Text;

namespace BlazorBase.User.SecurityHarness.IdentityProvider;

/// <summary>Unpadded base64url encoding, the segment encoding every compact JWS uses.</summary>
internal static class Base64UrlEncoding
{
    public static string Encode(string value) => Encode(Encoding.UTF8.GetBytes(value));

    public static string Encode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
