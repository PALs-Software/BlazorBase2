using Microsoft.AspNetCore.Http;

namespace BlazorBase.User.Server.OAuth.Endpoints;

/// <summary>
/// Marks every OAuth flow response <c>Cache-Control: no-store</c> and <c>Pragma: no-cache</c>, as
/// RFC 6749 section 5.1 requires for anything carrying a token, a code or a client secret.
/// </summary>
public sealed class OAuthNoStoreEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var headers = context.HttpContext.Response.Headers;
        headers.CacheControl = "no-store";
        headers.Pragma = "no-cache";

        return await next(context);
    }
}
