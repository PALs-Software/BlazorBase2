using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace BlazorBase.User.SecurityHarness.IdentityProvider;

/// <summary>
/// Two RSA key pairs the fake provider can publish to its JWKS or withhold independently, so key
/// rotation is simply publishing one and withholding the other, and a token signed with a withheld
/// key is the unknown-signing-key attack case. Key A is published and key B withheld at
/// construction time.
/// </summary>
public sealed class FakeSigningKeyRegistry : IDisposable
{
    public const string KeyIdA = "harness-key-a";
    public const string KeyIdB = "harness-key-b";

    private readonly ConcurrentDictionary<string, RSA> Keys = new();
    private readonly ConcurrentDictionary<string, bool> Published = new();

    public FakeSigningKeyRegistry()
    {
        Keys[KeyIdA] = RSA.Create(2048);
        Keys[KeyIdB] = RSA.Create(2048);
        Published[KeyIdA] = true;
        Published[KeyIdB] = false;
    }

    /// <summary>Every key the registry holds, regardless of publication state - a probe signs with a withheld key on purpose.</summary>
    public RSA GetKey(string kid) => Keys[kid];

    public void Publish(string kid) => Published[kid] = true;

    public void Withhold(string kid) => Published[kid] = false;

    /// <summary>The RFC 7517 JWKS document, containing only the currently published keys.</summary>
    public object BuildJwksDocument() => new
    {
        keys = Keys
            .Where(pair => Published.GetValueOrDefault(pair.Key))
            .Select(pair => BuildJsonWebKey(pair.Key, pair.Value))
            .ToArray(),
    };

    private static object BuildJsonWebKey(string kid, RSA key)
    {
        var parameters = key.ExportParameters(includePrivateParameters: false);

        return new
        {
            kty = "RSA",
            use = "sig",
            kid,
            alg = "RS256",
            n = Base64UrlEncoding.Encode(parameters.Modulus!),
            e = Base64UrlEncoding.Encode(parameters.Exponent!),
        };
    }

    public void Dispose()
    {
        foreach (var key in Keys.Values)
            key.Dispose();
    }
}
