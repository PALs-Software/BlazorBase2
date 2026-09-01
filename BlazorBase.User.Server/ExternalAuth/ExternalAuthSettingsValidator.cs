using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.ExternalAuth;

/// <summary>
/// Validates <see cref="ExternalAuthSettings"/> at application startup, so a missing authority or
/// audience fails fast rather than surfacing as every exchange request rejecting valid tokens.
/// </summary>
public class ExternalAuthSettingsValidator(IServiceScopeFactory serviceScopeFactory) : IValidateOptions<ExternalAuthSettings>
{
    #region Injects
    private readonly IServiceScopeFactory ServiceScopeFactory = serviceScopeFactory;
    #endregion

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, ExternalAuthSettings options)
    {
        if (string.IsNullOrWhiteSpace(options.Authority))
            return ValidateOptionsResult.Fail("ExternalAuthSettings.Authority must not be empty.");

        if (options.Audiences.Count == 0)
            return ValidateOptionsResult.Fail("ExternalAuthSettings.Audiences must contain at least one audience.");

        if (ExternalAuthSettings.IsMultiTenantAuthority(options.Authority) &&
            options.AllowedTenantIds.Count == 0 &&
            DefaultAllowListIsInUse())
            return ValidateOptionsResult.Fail(
                "ExternalAuthSettings.AllowedTenantIds must contain at least one tenant ID when Authority is a " +
                "multi-tenant endpoint (/common, /organizations or /consumers) and no custom IExternalTenantAllowList " +
                "is registered - otherwise a token from any Entra tenant in existence passes issuer validation.");

        return ValidateOptionsResult.Success;
    }

    /// <summary>
    /// A host that registered its own <see cref="IExternalTenantAllowList"/> is trusted to decide
    /// which tenants are acceptable itself, so the static-list requirement below applies only to
    /// <see cref="DefaultExternalTenantAllowList"/>. Resolved from a fresh scope rather than
    /// injected directly, since a host implementation may depend on a scoped service such as a
    /// database context.
    /// </summary>
    private bool DefaultAllowListIsInUse()
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var allowList = scope.ServiceProvider.GetRequiredService<IExternalTenantAllowList>();

        return allowList is DefaultExternalTenantAllowList;
    }
}
