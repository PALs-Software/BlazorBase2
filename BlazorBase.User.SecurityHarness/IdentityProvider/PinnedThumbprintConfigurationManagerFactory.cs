using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace BlazorBase.User.SecurityHarness.IdentityProvider;

/// <summary>
/// Builds the <see cref="BaseConfigurationManager"/> that <c>OidcExternalTokenValidator</c>
/// accepts as an optional constructor parameter, over an <see cref="HttpClient"/> that trusts
/// exactly the fake provider's certificate thumbprint - never a blanket <c>_ =&gt; true</c>
/// validation callback. Registering the resulting instance in the host's own service collection
/// before <c>AddBlazorBaseExternalAuth</c> is what lets the validator's real JWKS retrieval path
/// run against the fake provider's HTTPS endpoint without touching the validator itself.
/// </summary>
/// <remarks>
/// <see cref="Create"/> never touches the library's shipping defaults on its own: the manager it
/// returns keeps the built-in five-minute refresh throttle, and the process stays in the library's
/// default fire-and-forget refresh mode unless a caller deliberately opts in via
/// <see cref="SetBlockingRefreshMode"/>. With those shipping defaults, <c>ConfigurationManager</c>'s
/// refresh-on-signature-failure is fire-and-forget: a <c>RequestRefresh()</c> call triggered by a
/// failed validation returns immediately, so the retry inside that same failed validation call still
/// observes the stale configuration, and a just-rotated signing key is therefore not picked up on the
/// request that triggered the refresh - only on a later one, once the background fetch has completed.
/// A harness whose purpose is to observe real behaviour must produce that result by default, and must
/// only produce the repaired one when a caller asks for it by name.
/// </remarks>
public static class PinnedThumbprintConfigurationManagerFactory
{
    private const string BlockingRefreshSwitchName = "Switch.Microsoft.IdentityModel.UpdateConfigAsBlocking";

    /// <summary>
    /// Whether the process-wide <c>Switch.Microsoft.IdentityModel.UpdateConfigAsBlocking</c> AppContext
    /// switch is currently set. A probe or report reads this to state which refresh mode produced its
    /// result, since the switch affects every <see cref="BaseConfigurationManager"/> in the process, not
    /// only one built by <see cref="Create"/>.
    /// </summary>
    public static bool BlockingRefreshEnabled =>
        AppContext.TryGetSwitch(BlockingRefreshSwitchName, out var isEnabled) && isEnabled;

    /// <summary>
    /// Deliberately flips the process-wide <c>Switch.Microsoft.IdentityModel.UpdateConfigAsBlocking</c>
    /// AppContext switch, so that <c>RequestRefresh()</c> waits for the fetch to complete instead of
    /// returning immediately - the one supported, library-wide way to make key rotation observable on
    /// the very next request. This is never set as a side effect of building a manager; a caller that
    /// wants blocking refresh must call this explicitly and is then responsible for recording that the
    /// result it produces was measured in blocking mode, not the shipping default.
    /// </summary>
    public static void SetBlockingRefreshMode(bool enabled) => AppContext.SetSwitch(BlockingRefreshSwitchName, enabled);

    /// <summary>
    /// Builds a manager over an <see cref="HttpClient"/> pinned to <paramref name="certificate"/>'s
    /// thumbprint. With <paramref name="options"/> left <see langword="null"/>, the returned manager
    /// keeps every library default, including the five-minute refresh throttle.
    /// </summary>
    public static BaseConfigurationManager Create(
        string authority,
        X509Certificate2 certificate,
        PinnedThumbprintConfigurationManagerOptions? options = null)
    {
        options ??= new PinnedThumbprintConfigurationManagerOptions();
        var metadataAddress = $"{authority.TrimEnd('/')}/.well-known/openid-configuration";

        var manager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever(CreatePinnedHttpClient(certificate)) { RequireHttps = true });

        if (options.RefreshInterval is { } refreshInterval)
            manager.RefreshInterval = refreshInterval;

        return manager;
    }

    /// <summary>
    /// Publishes <paramref name="keyIdToPublish"/> and withholds <paramref name="keyIdToWithhold"/> on
    /// <paramref name="registry"/>, and sets the process-wide blocking-refresh switch to
    /// <paramref name="useBlockingRefresh"/> in the same call, since a key-rotation probe needs the
    /// provider's published key and the manager's refresh mode to change together to produce one
    /// coherent result. Call this once per mode a rotation probe wants to measure - once with
    /// <paramref name="useBlockingRefresh"/> <see langword="false"/> for the shipping-default result,
    /// and again with it <see langword="true"/> for the blocking-refresh result - and record
    /// <see cref="BlockingRefreshEnabled"/> alongside each result.
    /// </summary>
    public static void RotateSigningKey(
        FakeSigningKeyRegistry registry,
        string keyIdToPublish,
        string keyIdToWithhold,
        bool useBlockingRefresh)
    {
        SetBlockingRefreshMode(useBlockingRefresh);
        registry.Publish(keyIdToPublish);
        registry.Withhold(keyIdToWithhold);
    }

    private static HttpClient CreatePinnedHttpClient(X509Certificate2 certificate)
    {
        var pinnedThumbprint = certificate.Thumbprint;
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, presentedCertificate, _, _) =>
                presentedCertificate is not null &&
                string.Equals(presentedCertificate.Thumbprint, pinnedThumbprint, StringComparison.OrdinalIgnoreCase),
        };

        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
    }
}

/// <summary>
/// Explicit, opt-in overrides for <see cref="PinnedThumbprintConfigurationManagerFactory.Create"/>.
/// Every property defaults to the library's own shipping default, so a caller that does not set
/// anything gets exactly the behaviour <c>OidcExternalTokenValidator</c> would get in production.
/// </summary>
public sealed class PinnedThumbprintConfigurationManagerOptions
{
    /// <summary>
    /// Overrides the manager's refresh-on-signature-failure throttle, which defaults to five minutes
    /// in the underlying library. Left <see langword="null"/> by default so the manager keeps that
    /// library default; a probe that wants a rotated key observable within the run's lifetime sets
    /// this to a harness timescale explicitly, and must then treat the throttled, five-minute
    /// shipping default as the behaviour actually being measured everywhere else.
    /// </summary>
    public TimeSpan? RefreshInterval { get; init; }
}
