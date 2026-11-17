using BlazorBase.User.Server.OAuth.Protocol;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace BlazorBase.User.Server.OAuth.Endpoints;

/// <summary>
/// Builds OAuth error responses in the RFC 6749 section 5.2 JSON shape
/// <c>{ "error": "...", "error_description": "..." }</c>.
/// </summary>
public static class OAuthErrorResults
{
    /// <summary>
    /// Answers <paramref name="error"/> with 400, or with 401 for <c>invalid_client</c>; an
    /// <c>invalid_client</c> answer to a client that authenticated with HTTP Basic also carries
    /// <c>WWW-Authenticate: Basic</c>, as RFC 6749 section 5.2 requires.
    /// </summary>
    public static IResult Error(HttpContext httpContext, OAuthProtocolError error, bool usedBasicAuthentication = false)
    {
        var isInvalidClient = error.Error == OAuthErrorCodes.InvalidClient;

        if (isInvalidClient && usedBasicAuthentication)
            httpContext.Response.Headers.Append(HeaderNames.WWWAuthenticate, "Basic realm=\"oauth\"");

        return Results.Json(
            new Dictionary<string, string> { ["error"] = error.Error, ["error_description"] = error.Description },
            statusCode: isInvalidClient ? StatusCodes.Status401Unauthorized : StatusCodes.Status400BadRequest);
    }

    /// <summary>Shorthand for <see cref="Error(HttpContext, OAuthProtocolError, bool)"/> with a new error.</summary>
    public static IResult Error(HttpContext httpContext, string error, string description) =>
        Error(httpContext, new OAuthProtocolError(error, description));
}
