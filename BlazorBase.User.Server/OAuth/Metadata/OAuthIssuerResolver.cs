using BlazorBase.User.Server.OAuth.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.OAuth.Metadata;

/// <summary>
/// Resolves the issuer identifier - the base of every advertised URL and the <c>iss</c> value of every
/// authorization response - from <see cref="OAuthServerOptions.IssuerUri"/>, or from the request as
/// <c>{scheme}://{host}{pathBase}</c> when none is configured.
/// </summary>
public sealed class OAuthIssuerResolver(IOptionsMonitor<OAuthServerOptions> options)
{
    #region Injects
    private readonly IOptionsMonitor<OAuthServerOptions> Options = options;
    #endregion

    /// <summary>Returns the issuer for <paramref name="request"/>, never with a trailing slash.</summary>
    public string Resolve(HttpRequest request)
    {
        var configuredIssuer = Options.CurrentValue.NormalizedIssuerUri;

        if (configuredIssuer is not null)
            return configuredIssuer;

        return $"{request.Scheme}://{request.Host.ToUriComponent()}{request.PathBase.ToUriComponent()}".TrimEnd('/');
    }
}
