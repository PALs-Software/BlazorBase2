using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.ExternalAuth;

/// <summary>
/// Answers <see cref="IExternalTenantAllowList"/> from the static
/// <see cref="ExternalAuthSettings.AllowedTenantIds"/> list. Registered by
/// <c>AddBlazorBaseExternalAuth</c> with <c>TryAdd…</c>, so a host that needs its allowed tenants
/// to come from a database instead registers its own implementation first.
/// </summary>
public class DefaultExternalTenantAllowList(IOptions<ExternalAuthSettings> settings) : IExternalTenantAllowList
{
    #region Injects
    private readonly ExternalAuthSettings Settings = settings.Value;
    #endregion

    public Task<bool> IsAllowedAsync(string tenantId, CancellationToken cancellationToken)
    {
        var allowed = Settings.AllowedTenantIds.Count == 0 ||
            Settings.AllowedTenantIds.Contains(tenantId, StringComparer.OrdinalIgnoreCase);

        return Task.FromResult(allowed);
    }
}
