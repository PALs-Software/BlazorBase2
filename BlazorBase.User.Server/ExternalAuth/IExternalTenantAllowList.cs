namespace BlazorBase.User.Server.ExternalAuth;

/// <summary>
/// Decides whether a tenant ID (an external token's <c>tid</c> claim) may exchange a token for
/// this application, consulted once per validated token by <see cref="OidcExternalTokenValidator"/>.
/// The default implementation (<see cref="DefaultExternalTenantAllowList"/>) answers from the
/// static <see cref="ExternalAuthSettings.AllowedTenantIds"/> list; a genuinely multi-tenant SaaS
/// host - where the acceptable tenants are rows in its own database rather than configuration -
/// replaces it with its own implementation, registered before calling
/// <c>AddBlazorBaseExternalAuth</c> so the framework's <c>TryAdd</c> registration does not
/// overwrite it.
/// </summary>
public interface IExternalTenantAllowList
{
    Task<bool> IsAllowedAsync(string tenantId, CancellationToken cancellationToken);
}
