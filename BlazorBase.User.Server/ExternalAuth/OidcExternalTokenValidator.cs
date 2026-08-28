using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace BlazorBase.User.Server.ExternalAuth;

/// <summary>
/// Validates an external token against an OIDC authority's published signing keys - fetched and
/// cached by <see cref="ConfigurationManager{T}"/>, not trusted from the token itself. The token
/// is never hand-parsed for its claims before its signature, audience and lifetime are checked.
/// </summary>
public class OidcExternalTokenValidator(
    IOptions<ExternalAuthSettings> settings,
    ILogger<OidcExternalTokenValidator> logger) : IExternalTokenValidator
{
    #region Injects
    private readonly ExternalAuthSettings Settings = settings.Value;
    private readonly ILogger<OidcExternalTokenValidator> Logger = logger;
    #endregion

    private readonly ConfigurationManager<OpenIdConnectConfiguration> OidcConfigurationManager = new(
        $"{settings.Value.Authority.TrimEnd('/')}/.well-known/openid-configuration",
        new OpenIdConnectConfigurationRetriever());

    public async Task<ClaimsPrincipal?> ValidateAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            var configuration = await OidcConfigurationManager.GetConfigurationAsync(cancellationToken);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = configuration.SigningKeys,
                ValidateIssuer = Settings.ValidateIssuer,
                ValidIssuer = configuration.Issuer,
                ValidateAudience = true,
                ValidAudiences = Settings.Audiences,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            };

            var handler = new JwtSecurityTokenHandler();
            return handler.ValidateToken(token, validationParameters, out _);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "External token validation failed.");
            return null;
        }
    }
}
