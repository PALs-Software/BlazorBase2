using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BlazorBase.User.SecurityHarness.IdentityProvider;

/// <summary>
/// Hand-assembles a compact JWS from a <see cref="FakeTokenDescription"/> by building and encoding
/// the header and payload segments directly, rather than emitting a valid token with a library and
/// patching its bytes. The malformed shapes this exists for - an empty signature segment, a
/// mismatched <c>kid</c>, an HMAC secret built from an RSA public key - are not shapes a
/// well-behaved token handler will produce at all.
/// </summary>
public static class FakeTokenBuilder
{
    /// <summary>An <c>alg: none</c> token with no signature segment at all.</summary>
    public static string CreateUnsignedToken(FakeTokenDescription description) =>
        $"{BuildSigningInput(description, "none", kid: null)}.";

    /// <summary>
    /// A correctly RS256-signed token. <paramref name="kid"/> and <paramref name="signingKey"/> are
    /// independent parameters, so a caller can name one key in the header while signing with
    /// another - the <c>kid</c>-mismatch attack case.
    /// </summary>
    public static string CreateRsaSignedToken(FakeTokenDescription description, string kid, RSA signingKey)
    {
        var signingInput = BuildSigningInput(description, "RS256", kid);
        var signature = signingKey.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{signingInput}.{Base64UrlEncoding.Encode(signature)}";
    }

    /// <summary>
    /// The algorithm-confusion attack: an <c>HS256</c> token whose HMAC secret is
    /// <paramref name="hmacSecret"/> - passing the provider's own RSA public key bytes in here is
    /// exactly what makes that attack representable.
    /// </summary>
    public static string CreateHmacSignedToken(FakeTokenDescription description, string kid, byte[] hmacSecret)
    {
        var signingInput = BuildSigningInput(description, "HS256", kid);
        using var hmac = new HMACSHA256(hmacSecret);
        var signature = hmac.ComputeHash(Encoding.ASCII.GetBytes(signingInput));

        return $"{signingInput}.{Base64UrlEncoding.Encode(signature)}";
    }

    private static string BuildSigningInput(FakeTokenDescription description, string algorithm, string? kid)
    {
        var header = kid is null
            ? $$"""{"alg":"{{algorithm}}","typ":"JWT"}"""
            : $$"""{"alg":"{{algorithm}}","typ":"JWT","kid":"{{kid}}"}""";

        return $"{Base64UrlEncoding.Encode(header)}.{Base64UrlEncoding.Encode(BuildPayloadJson(description))}";
    }

    private static string BuildPayloadJson(FakeTokenDescription description)
    {
        var claims = new Dictionary<string, object?>
        {
            ["iss"] = description.Issuer,
            ["aud"] = description.Audience,
            ["sub"] = description.Subject,
        };

        if (description.TenantId is not null)
            claims["tid"] = description.TenantId;

        if (description.ObjectId is not null)
            claims["oid"] = description.ObjectId;

        if (description.Email is not null)
            claims["email"] = description.Email;

        if (description.EmailVerified is not null)
            claims["email_verified"] = description.EmailVerified.Value;

        if (description.Scopes is not null)
            claims["scp"] = description.Scopes;

        if (description.Roles is { Count: > 0 })
            claims["roles"] = description.Roles;

        if (description.IssuedAt is not null)
            claims["iat"] = description.IssuedAt.Value.ToUnixTimeSeconds();

        if (description.NotBefore is not null)
            claims["nbf"] = description.NotBefore.Value.ToUnixTimeSeconds();

        if (description.ExpiresAt is not null)
            claims["exp"] = description.ExpiresAt.Value.ToUnixTimeSeconds();

        foreach (var (key, value) in description.ExtraClaims ?? new Dictionary<string, object>())
            claims[key] = value;

        return JsonSerializer.Serialize(claims);
    }
}
