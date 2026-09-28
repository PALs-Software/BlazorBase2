namespace BlazorBase.User.SecurityHarness.IdentityProvider;

/// <summary>
/// Every claim <see cref="FakeTokenBuilder"/> can place into a hand-assembled token. Everything
/// beyond <see cref="Issuer"/>, <see cref="Audience"/> and <see cref="Subject"/> is optional, so a
/// probe can construct exactly the malformed shape an attack needs - a token with no <c>exp</c>, no
/// <c>tid</c>, or an oversized extra claim.
/// </summary>
public sealed class FakeTokenDescription
{
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string Subject { get; init; }
    public string? TenantId { get; init; }
    public string? ObjectId { get; init; }
    public string? Email { get; init; }
    public bool? EmailVerified { get; init; }
    public string? Scopes { get; init; }
    public IReadOnlyList<string>? Roles { get; init; }
    public DateTimeOffset? IssuedAt { get; init; }
    public DateTimeOffset? NotBefore { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Claims beyond the named ones above, for example an oversized value for the denial-of-service case.</summary>
    public IReadOnlyDictionary<string, object>? ExtraClaims { get; init; }
}
