using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;
using BlazorBase.User.Server.AccessTokens;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.SecurityHarness.Probes.AccessTokens;

/// <summary>
/// Shared plumbing every access-token probe uses: minting and revoking tokens directly through
/// <see cref="IAccessTokenService"/> against the profile's own service provider - never through an
/// HTTP endpoint, since the framework does not map one for this - and calling the harness's own
/// endpoint gated behind <see cref="AccessTokenAuthenticationDefaults.AuthenticationScheme"/>
/// (<see cref="Hosting.HarnessHostFactory"/>'s <c>harness/access-token-probe</c>), the only route in
/// the harness that authenticates with that scheme rather than the default JWT one.
/// </summary>
internal static class AccessTokenSupport
{
    public static async Task<CreatedAccessToken> CreateTokenAsync(ProbeContext context, string name, DateTime? expiresAt = null, string? userId = null, CancellationToken cancellationToken = default)
    {
        using var scope = context.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAccessTokenService>();
        return await service.CreateAsync(name, expiresAt, userId, cancellationToken);
    }

    public static async Task<bool> RevokeTokenAsync(ProbeContext context, Guid tokenId, CancellationToken cancellationToken = default)
    {
        using var scope = context.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAccessTokenService>();
        return await service.RevokeAsync(tokenId, cancellationToken: cancellationToken);
    }

    public static async Task SetUserActiveAsync(ProbeContext context, string userId, bool isActive)
    {
        using var scope = context.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<HarnessUser>>();
        var user = await userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException($"User '{userId}' vanished before its active flag could be changed.");
        user.IsActive = isActive;
        var result = await userManager.UpdateAsync(user);

        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not update user '{userId}': {string.Join("; ", result.Errors.Select(error => error.Description))}");
    }

    public static Task<ProbeExchange> CallAccessTokenProbeEndpointAsync(HttpClient client, Uri baseAddress, string? secret, CancellationToken cancellationToken) =>
        CoreSessionSupport.SendAsync(client, HttpMethod.Get, baseAddress, "harness/access-token-probe", bearerToken: secret, cancellationToken: cancellationToken);

    public static async Task<DateTime?> GetLastUsedAtAsync(ProbeContext context, Guid tokenId, CancellationToken cancellationToken = default)
    {
        using var scope = context.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAccessTokenService>();
        var summaries = await service.ListAsync(cancellationToken: cancellationToken);
        return summaries.Single(summary => summary.Id == tokenId).LastUsedAt;
    }
}
