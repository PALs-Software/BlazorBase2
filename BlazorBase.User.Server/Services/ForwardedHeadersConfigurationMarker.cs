namespace BlazorBase.User.Server.Services;

/// <summary>
/// Records whether <c>AddBlazorBaseForwardedHeaders</c> was called with at least one known proxy or
/// network, so <see cref="BlazorBaseForwardedHeadersStartupGuardFilter"/> can tell that from a host
/// that never called it at all - both leave <see cref="ForwardedHeadersConfigurationMarker"/>
/// unregistered or registered with nothing trusted, which the filter treats identically as
/// "not configured".
/// </summary>
internal sealed class ForwardedHeadersConfigurationMarker(bool hasKnownProxiesOrNetworks)
{
    public bool HasKnownProxiesOrNetworks { get; } = hasKnownProxiesOrNetworks;
}
