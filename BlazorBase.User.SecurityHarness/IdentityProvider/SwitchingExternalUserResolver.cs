using System.Security.Claims;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.Server.ExternalAuth;

namespace BlazorBase.User.SecurityHarness.IdentityProvider;

/// <summary>
/// Delegates to <see cref="StrictSubjectExternalUserResolver"/> or
/// <see cref="NaiveEmailMatchingExternalUserResolver"/> depending on <see cref="ExternalUserResolverSwitch.Mode"/>,
/// so a probe can compare the two resolution strategies against the same running host.
/// </summary>
internal sealed class SwitchingExternalUserResolver(
    ExternalUserResolverSwitch resolverSwitch,
    StrictSubjectExternalUserResolver strictResolver,
    NaiveEmailMatchingExternalUserResolver naiveResolver) : IExternalUserResolver<HarnessUser>
{
    public Task<ExternalUserResolution<HarnessUser>> ResolveAsync(ClaimsPrincipal externalPrincipal, CancellationToken cancellationToken) =>
        resolverSwitch.Mode == ExternalUserResolverMode.Naive
            ? naiveResolver.ResolveAsync(externalPrincipal, cancellationToken)
            : strictResolver.ResolveAsync(externalPrincipal, cancellationToken);
}
