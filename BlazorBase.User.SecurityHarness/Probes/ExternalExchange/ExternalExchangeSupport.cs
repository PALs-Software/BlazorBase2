using System.Security.Cryptography;
using System.Text;
using BlazorBase.User.SecurityHarness.IdentityProvider;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;
using BlazorBase.User.Server.ExternalAuth;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>
/// Shared plumbing every external-token-exchange probe uses: reaching the fake identity provider
/// behind <c>context.Services</c>, building the baseline valid <see cref="FakeTokenDescription"/>
/// every attack mutates one field of, posting the exchange request, and decoding a token's header
/// and payload back into readable <see cref="ProbeEvidence"/> - a rejection with no visible token
/// is not usable in a review.
/// </summary>
internal static class ExternalExchangeSupport
{
    /// <summary>Matches <c>ExternalAuthSettings:Audiences:0</c> as configured by <see cref="Hosting.HarnessHostFactory"/>.</summary>
    public const string Audience = "blazorbase-security-harness";

    /// <summary>The subject a probe's own valid tokens carry unless it deliberately varies it.</summary>
    public const string DefaultSubject = "harness-external-subject@harness.local";

    public static FakeIdentityProviderHost GetProviderHost(ProbeContext context) =>
        context.Services.GetRequiredService<FakeIdentityProviderHost>();

    /// <summary>
    /// Seeds a local user whose username is <paramref name="subject"/>, so
    /// <c>StrictSubjectExternalUserResolver</c> - the resolver every profile in this category runs
    /// with by default - can resolve a token's <c>oid</c>/<c>sub</c> to a real local account. Without
    /// this, every probe's accept/reject outcome would actually be decided by "no such local user",
    /// masking whatever the token validator itself would have decided.
    /// </summary>
    public static Task EnsureResolvableSubjectAsync(ProbeContext context, string subject = DefaultSubject) =>
        CoreSessionSupport.SeedUserAsync(context, subject);

    /// <summary>
    /// A token description that <see cref="OidcExternalTokenValidator"/> accepts unmodified: signed
    /// by the currently published key, the allowed tenant, the configured audience, and a lifetime
    /// comfortably inside its validity window. Every attack probe starts from this and overrides
    /// exactly the one parameter its attack is about - <paramref name="issuerTenantId"/> controls
    /// which tenant's issuer URL the token claims to come from (and therefore what the correct
    /// signature must be checked against), while <paramref name="tidClaim"/> is the <c>tid</c> claim
    /// itself, kept independent so a forged-tenant attack can present a legitimate issuer alongside a
    /// disallowed or missing <c>tid</c>.
    /// </summary>
    public static FakeTokenDescription CreateValidDescription(
        FakeIdentityProviderHost providerHost,
        string subject = DefaultSubject,
        string? issuerTenantId = FakeIdentityProviderTenants.AllowedTenantId,
        string? tidClaim = FakeIdentityProviderTenants.AllowedTenantId,
        string? audience = null,
        string? issuer = null,
        string? scopes = null,
        IReadOnlyList<string>? roles = null,
        string? email = null,
        bool? emailVerified = null,
        DateTimeOffset? issuedAt = null,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? expiresAt = null,
        bool includeExpiry = true,
        IReadOnlyDictionary<string, object>? extraClaims = null)
    {
        var now = DateTimeOffset.UtcNow;

        return new FakeTokenDescription
        {
            Issuer = issuer ?? providerHost.TenantAuthority(issuerTenantId ?? FakeIdentityProviderTenants.AllowedTenantId),
            Audience = audience ?? Audience,
            Subject = subject,
            TenantId = tidClaim,
            ObjectId = subject,
            Scopes = scopes,
            Roles = roles,
            Email = email,
            EmailVerified = emailVerified,
            IssuedAt = issuedAt ?? now.AddMinutes(-1),
            NotBefore = notBefore ?? now.AddMinutes(-1),
            ExpiresAt = includeExpiry ? (expiresAt ?? now.AddMinutes(10)) : null,
            ExtraClaims = extraClaims,
        };
    }

    /// <summary>Signs <paramref name="description"/> with the provider's key A, correctly, under key A's own <c>kid</c>.</summary>
    public static string SignWithKeyA(FakeIdentityProviderHost providerHost, FakeTokenDescription description) =>
        FakeTokenBuilder.CreateRsaSignedToken(description, FakeSigningKeyRegistry.KeyIdA, providerHost.KeyRegistry.GetKey(FakeSigningKeyRegistry.KeyIdA));

    public static async Task<ProbeExchange> ExchangeAsync(HttpClient client, Uri baseAddress, string token, CancellationToken cancellationToken) =>
        await CoreSessionSupport.SendAsync(client, HttpMethod.Post, baseAddress, "api/auth/external/exchange", new ExternalTokenExchangeRequest { ExternalToken = token }, cancellationToken: cancellationToken);

    /// <summary>Calls <c>harness/whoami</c> with the given access token, to confirm which local account a set of minted tokens actually belongs to.</summary>
    public static async Task<ProbeExchange> WhoAmIAsync(HttpClient client, Uri baseAddress, string accessToken, CancellationToken cancellationToken) =>
        await CoreSessionSupport.SendAsync(client, HttpMethod.Get, baseAddress, "harness/whoami", bearerToken: accessToken, cancellationToken: cancellationToken);

    /// <summary>
    /// One rejection probe's evidence: the token it sent, decoded header and payload, and the
    /// response it got back - never a bare status code with no visible token.
    /// </summary>
    public static ProbeEvidence BuildEvidence(string description, string token, ProbeExchange exchange) =>
        new(description, Request: $"POST api/auth/external/exchange\n{DescribeToken(token)}", Response: $"{(int)exchange.StatusCode} {exchange.StatusCode}\n{exchange.ResponseBody}");

    /// <summary>Decodes a compact JWS's header and payload segments back into readable JSON, tolerating a token with fewer than three segments.</summary>
    public static string DescribeToken(string token)
    {
        var segments = token.Split('.');
        var header = segments.Length > 0 ? DecodeSegment(segments[0]) : "<missing>";
        var payload = segments.Length > 1 ? DecodeSegment(segments[1]) : "<missing>";
        var signaturePresent = segments.Length > 2 && segments[2].Length > 0;

        return $"Header: {header}\nPayload: {payload}\nSignature present: {signaturePresent}";
    }

    /// <summary>
    /// Every encoding of an RSA public key's material this harness tries as an HMAC secret for the
    /// algorithm-confusion attack - a single attempt proves less than several, since a verifier that
    /// happens to feed the key through <c>RSA.ExportRSAPublicKey()</c> internally would only be
    /// caught by the matching encoding.
    /// </summary>
    public static IReadOnlyList<(string EncodingName, byte[] Secret)> BuildAlgConfusionSecrets(RSA publicKey)
    {
        var modulus = publicKey.ExportParameters(includePrivateParameters: false).Modulus!;
        var spkiDer = publicKey.ExportSubjectPublicKeyInfo();
        var spkiPem = publicKey.ExportSubjectPublicKeyInfoPem();

        return
        [
            ("raw RSA modulus bytes", modulus),
            ("SPKI DER bytes", spkiDer),
            ("SPKI PEM text (UTF-8)", Encoding.UTF8.GetBytes(spkiPem)),
            ("base64 of the raw modulus (UTF-8 text)", Encoding.UTF8.GetBytes(Convert.ToBase64String(modulus))),
            ("base64 of the SPKI DER (UTF-8 text)", Encoding.UTF8.GetBytes(Convert.ToBase64String(spkiDer))),
        ];
    }

    private static string DecodeSegment(string segment)
    {
        if (segment.Length == 0)
            return "<empty>";

        try
        {
            return Encoding.UTF8.GetString(Base64UrlEncoding.Decode(segment));
        }
        catch (FormatException)
        {
            return $"<not valid base64url: {segment}>";
        }
    }
}
