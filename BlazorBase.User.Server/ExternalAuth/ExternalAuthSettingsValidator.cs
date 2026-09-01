using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.ExternalAuth;

/// <summary>
/// Validates <see cref="ExternalAuthSettings"/> at application startup, so a missing authority or
/// audience fails fast rather than surfacing as every exchange request rejecting valid tokens.
/// </summary>
public class ExternalAuthSettingsValidator : IValidateOptions<ExternalAuthSettings>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, ExternalAuthSettings options)
    {
        if (string.IsNullOrWhiteSpace(options.Authority))
            return ValidateOptionsResult.Fail("ExternalAuthSettings.Authority must not be empty.");

        if (options.Audiences.Count == 0)
            return ValidateOptionsResult.Fail("ExternalAuthSettings.Audiences must contain at least one audience.");

        if (ExternalAuthSettings.IsMultiTenantAuthority(options.Authority) && options.AllowedTenantIds.Count == 0)
            return ValidateOptionsResult.Fail(
                "ExternalAuthSettings.AllowedTenantIds must contain at least one tenant ID when Authority is a " +
                "multi-tenant endpoint (/common, /organizations or /consumers) - otherwise a token from any Entra " +
                "tenant in existence passes issuer validation.");

        return ValidateOptionsResult.Success;
    }
}
