using System.Security.Cryptography;
using System.Text.Json;
using BlazorBase.User.Server.OAuth.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.OAuth.Authorization;

/// <summary>
/// Seals an <see cref="OAuthAuthorizationRequest"/> into the URL-safe <c>request</c> parameter of the
/// consent page with an <see cref="ITimeLimitedDataProtector"/>, valid for
/// <see cref="OAuthServerOptions.AuthorizationRequestLifetime"/>.
/// </summary>
public sealed class OAuthAuthorizationRequestProtector(
    IDataProtectionProvider dataProtectionProvider,
    IOptionsMonitor<OAuthServerOptions> options)
{
    /// <summary>The data-protection purpose; bump the version suffix when the record changes shape.</summary>
    public const string Purpose = "BlazorBase.User.Server.OAuth.AuthorizationRequest.v1";

    #region Injects
    private readonly ITimeLimitedDataProtector Protector = dataProtectionProvider.CreateProtector(Purpose).ToTimeLimitedDataProtector();
    private readonly IOptionsMonitor<OAuthServerOptions> Options = options;
    #endregion

    /// <summary>Returns the protected, Base64Url form of <paramref name="request"/>.</summary>
    public string Protect(OAuthAuthorizationRequest request) =>
        Protector.Protect(JsonSerializer.Serialize(request), Options.CurrentValue.AuthorizationRequestLifetime);

    /// <summary>Returns the request, or <see langword="null"/> when the value is expired, altered or not a request at all.</summary>
    public OAuthAuthorizationRequest? Unprotect(string? protectedRequest)
    {
        if (string.IsNullOrWhiteSpace(protectedRequest))
            return null;

        try
        {
            return JsonSerializer.Deserialize<OAuthAuthorizationRequest>(Protector.Unprotect(protectedRequest, out _));
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
