using System.Security.Cryptography.X509Certificates;

namespace BlazorBase.User.SecurityHarness.Hosting;

/// <summary>
/// Everything a <see cref="HarnessHostFactory"/> call needs that is shared across every profile
/// started in one harness run.
/// </summary>
public sealed class HarnessHostOptions
{
    /// <summary>Per-run scratch directory; each profile gets its own file-backed SQLite database inside it.</summary>
    public required string TempDirectory { get; init; }

    /// <summary>
    /// The fake identity provider's authority URL, supplied at host-construction time. Left
    /// <see langword="null"/> until step 2 wires the provider in - the <see cref="HarnessProfile.External"/>
    /// profile reports itself as <see cref="HarnessHostStatus.Unavailable"/> until then.
    /// </summary>
    public string? ExternalAuthority { get; init; }

    /// <summary>
    /// The tenant ID the <see cref="HarnessProfile.External"/> profile's <c>ExternalAuthSettings:AllowedTenantIds</c>
    /// allow-lists, so "allowed tenant" and "some other real tenant" are both representable against
    /// the fake identity provider's issuer.
    /// </summary>
    public string? ExternalAllowedTenantId { get; init; }

    /// <summary>The self-signed certificate every HTTPS profile listens with.</summary>
    public required X509Certificate2 Certificate { get; init; }

    /// <summary>
    /// Configuration values applied after the factory's own defaults (such as
    /// <c>AuthTokens:AccessTokenMinutes</c>), so a caller can override any of them for a run - for
    /// example to mint short-lived tokens or alter rate limits for a specific probe group.
    /// </summary>
    public IReadOnlyDictionary<string, string?> ConfigurationOverrides { get; init; } = new Dictionary<string, string?>();

    /// <summary>
    /// Overrides the per-profile database file name (normally <c>"{profile}.db"</c>). A probe that
    /// starts its own, differently-configured instance of a profile - to observe a shortened token
    /// lifetime or lockout window without a real wait, or to keep a rate-limit probe's budget from
    /// leaking into another probe's - supplies a unique name so its private Kestrel instance never
    /// shares a SQLite file with the main run's instance of the same profile.
    /// </summary>
    public string? DatabaseFileName { get; init; }
}
