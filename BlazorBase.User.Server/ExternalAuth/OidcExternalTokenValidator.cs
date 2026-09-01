using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Validators;

namespace BlazorBase.User.Server.ExternalAuth;

/// <summary>
/// Validates an external token against an OIDC authority's published signing keys - fetched,
/// cached and refreshed-on-signature-failure by a <see cref="BaseConfigurationManager"/>, never
/// trusted from the token itself. Issuer validation is never disabled: a multi-tenant authority is
/// checked with <see cref="AadIssuerValidator"/> against the tenant issuer template and the
/// token's own <c>tid</c>, and that <c>tid</c> must additionally be allowed by
/// <see cref="IExternalTenantAllowList"/> - the issuer template alone accepts a token from any
/// Entra tenant in existence. The token is never hand-parsed for its claims before its signature,
/// audience and lifetime are checked.
/// </summary>
public class OidcExternalTokenValidator(
    IOptions<ExternalAuthSettings> settings,
    ILogger<OidcExternalTokenValidator> logger,
    IServiceScopeFactory serviceScopeFactory,
    BaseConfigurationManager? configurationManager = null) : IExternalTokenValidator
{
    #region Injects
    private readonly ExternalAuthSettings Settings = settings.Value;
    private readonly ILogger<OidcExternalTokenValidator> Logger = logger;
    private readonly IServiceScopeFactory ServiceScopeFactory = serviceScopeFactory;
    #endregion

    // Overridable so a test can substitute a StaticConfigurationManager<T> instead of one that
    // fetches metadata over the network.
    private readonly BaseConfigurationManager OidcConfigurationManager = configurationManager ?? new ConfigurationManager<OpenIdConnectConfiguration>(
        $"{settings.Value.Authority.TrimEnd('/')}/.well-known/openid-configuration",
        new OpenIdConnectConfigurationRetriever());

    public async Task<ClaimsPrincipal?> ValidateAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, BuildValidationParameters());

            if (!result.IsValid)
            {
                Logger.LogWarning(result.Exception, "External token validation failed.");
                return null;
            }

            var identity = result.ClaimsIdentity;
            var tenantId = identity.FindFirst("tid")?.Value;

            if (tenantId is null || !await TenantIsAllowedAsync(tenantId, cancellationToken))
            {
                Logger.LogWarning("External token rejected: tenant '{TenantId}' is not in the allowed tenant list.", tenantId);
                return null;
            }

            if (!HasRequiredScopeOrRole(identity))
            {
                Logger.LogWarning("External token rejected: it carries none of the required scopes or app roles.");
                return null;
            }

            return new ClaimsPrincipal(identity);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "External token validation failed.");
            return null;
        }
    }

    /// <summary>
    /// <see cref="TokenValidationParameters.ConfigurationManager"/> is set instead of copying
    /// signing keys out of a once-fetched configuration, so a signing-key rollover at the
    /// authority triggers an automatic refresh-on-signature-failure with a last-known-good
    /// fallback, rather than every exchange failing until the manager's own refresh interval
    /// elapses. <see cref="TokenValidationParameters.RefreshBeforeValidation"/> stays off -
    /// refreshing unconditionally on every request would defeat that caching, since the
    /// refresh-on-failure path already covers a rollover.
    /// </summary>
    private TokenValidationParameters BuildValidationParameters()
    {
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudiences = Settings.Audiences,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ConfigurationManager = OidcConfigurationManager,
            RefreshBeforeValidation = false,
        };

        if (ExternalAuthSettings.IsMultiTenantAuthority(Settings.Authority))
            validationParameters.IssuerValidator = AadIssuerValidator.GetAadIssuerValidator(Settings.Authority).Validate;

        return validationParameters;
    }

    /// <summary>
    /// Checked in addition to - never instead of - <see cref="AadIssuerValidator"/>: the issuer
    /// template it validates against matches every Entra tenant, so without this a multi-tenant
    /// authority accepts a token from any organization in the world. <see cref="IExternalTenantAllowList"/>
    /// is resolved from a fresh scope per call rather than injected directly - a host-backed
    /// implementation typically needs a scoped dependency such as a database context, and this
    /// validator itself is registered as a singleton so its configuration manager cache survives
    /// across requests.
    /// </summary>
    private async Task<bool> TenantIsAllowedAsync(string tenantId, CancellationToken cancellationToken)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var allowList = scope.ServiceProvider.GetRequiredService<IExternalTenantAllowList>();

        return await allowList.IsAllowedAsync(tenantId, cancellationToken);
    }

    private bool HasRequiredScopeOrRole(ClaimsIdentity identity)
    {
        if (Settings.RequiredScopes.Count == 0 && Settings.RequiredAppRoles.Count == 0)
            return true;

        var scopes = identity.FindFirst("scp")?.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];

        if (Settings.RequiredScopes.Any(required => scopes.Contains(required, StringComparer.Ordinal)))
            return true;

        var roles = identity.FindAll("roles").Select(claim => claim.Value);

        return Settings.RequiredAppRoles.Any(required => roles.Contains(required, StringComparer.Ordinal));
    }
}
