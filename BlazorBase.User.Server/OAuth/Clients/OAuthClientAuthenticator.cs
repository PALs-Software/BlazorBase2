using System.Text;
using BlazorBase.User.Server.OAuth.Protocol;
using Microsoft.AspNetCore.Http;

namespace BlazorBase.User.Server.OAuth.Clients;

/// <summary>
/// Authenticates the client calling the token or revocation endpoint by the method it registered:
/// <c>none</c> sends only <c>client_id</c>, <c>client_secret_post</c> sends id and secret as form fields,
/// <c>client_secret_basic</c> sends them form-urlencoded in an HTTP Basic header. Secrets are compared
/// by hash in constant time, and a method other than the registered one is rejected.
/// </summary>
public sealed class OAuthClientAuthenticator(IOAuthClientService clientService)
{
    #region Injects
    private readonly IOAuthClientService ClientService = clientService;
    #endregion

    private const string BasicSchemePrefix = "Basic ";
    private const string AuthenticationFailed = "Client authentication failed.";

    /// <summary>Authenticates the client of <paramref name="request"/>, whose form is <paramref name="form"/>.</summary>
    public async Task<OAuthClientAuthenticationResult> AuthenticateAsync(HttpRequest request, IFormCollection form, CancellationToken cancellationToken)
    {
        var authorizationHeader = request.Headers.Authorization.ToString();
        var usedBasic = authorizationHeader.StartsWith(BasicSchemePrefix, StringComparison.OrdinalIgnoreCase);

        if (!OAuthParameterReader.TryReadSingle(form["client_id"], out var formClientId) ||
            !OAuthParameterReader.TryReadSingle(form["client_secret"], out var formClientSecret))
            return OAuthClientAuthenticationResult.Failure(OAuthErrorCodes.InvalidRequest, "client_id and client_secret must not be repeated.", usedBasic);

        string? basicClientId = null;
        string? basicClientSecret = null;

        if (usedBasic && !TryReadBasicCredentials(authorizationHeader[BasicSchemePrefix.Length..].Trim(), out basicClientId, out basicClientSecret))
            return OAuthClientAuthenticationResult.Failure(OAuthErrorCodes.InvalidClient, "The Basic authorization header is malformed.", usedBasic);

        if (usedBasic && formClientSecret is not null)
            return OAuthClientAuthenticationResult.Failure(OAuthErrorCodes.InvalidRequest, "Use exactly one client authentication method.", usedBasic);

        if (usedBasic && formClientId is not null && formClientId != basicClientId)
            return OAuthClientAuthenticationResult.Failure(OAuthErrorCodes.InvalidClient, AuthenticationFailed, usedBasic);

        var clientId = usedBasic ? basicClientId : formClientId;

        if (string.IsNullOrEmpty(clientId))
            return OAuthClientAuthenticationResult.Failure(OAuthErrorCodes.InvalidClient, "client_id is required.", usedBasic);

        var client = await ClientService.FindAsync(clientId, cancellationToken);

        if (client is null)
            return OAuthClientAuthenticationResult.Failure(OAuthErrorCodes.InvalidClient, AuthenticationFailed, usedBasic);

        var authenticated = client.TokenEndpointAuthMethod switch
        {
            OAuthClientAuthenticationMethods.None => formClientSecret is null && string.IsNullOrEmpty(basicClientSecret),
            OAuthClientAuthenticationMethods.ClientSecretPost => !usedBasic && SecretMatches(formClientSecret, client.ClientSecretHash),
            OAuthClientAuthenticationMethods.ClientSecretBasic => usedBasic && SecretMatches(basicClientSecret, client.ClientSecretHash),
            _ => false,
        };

        if (!authenticated)
            return OAuthClientAuthenticationResult.Failure(OAuthErrorCodes.InvalidClient, AuthenticationFailed, usedBasic);

        return OAuthClientAuthenticationResult.Success(client, usedBasic);
    }

    private static bool SecretMatches(string? presentedSecret, string? storedHash)
    {
        if (string.IsNullOrEmpty(presentedSecret) || string.IsNullOrEmpty(storedHash))
            return false;

        return OAuthSecretGenerator.MatchesHash(presentedSecret, storedHash);
    }

    /// <summary>
    /// Decodes <c>base64(urlencode(id) ":" urlencode(secret))</c> - RFC 6749 section 2.3.1 requires both
    /// halves to be form-urlencoded before they are joined, so a colon inside either survives.
    /// </summary>
    private static bool TryReadBasicCredentials(string encodedCredentials, out string? clientId, out string? clientSecret)
    {
        clientId = null;
        clientSecret = null;

        string decodedCredentials;

        try
        {
            decodedCredentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
        }
        catch (FormatException)
        {
            return false;
        }

        var separatorIndex = decodedCredentials.IndexOf(':');

        if (separatorIndex < 0)
            return false;

        clientId = FormUrlDecode(decodedCredentials[..separatorIndex]);
        clientSecret = FormUrlDecode(decodedCredentials[(separatorIndex + 1)..]);
        return true;
    }

    private static string FormUrlDecode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));
}
