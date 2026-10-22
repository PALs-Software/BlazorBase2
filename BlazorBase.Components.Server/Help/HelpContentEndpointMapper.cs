using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace BlazorBase.Components.Server.Help;

/// <summary>
/// Serves the help content of the contextual help panel — Markdown and the images it shows — to signed-in users only,
/// instead of shipping it as anonymous static web assets.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><c>GET {RequestPath}/{culture}/{path}</c> returns a file of the content root; only Markdown and images are
/// served and nothing outside the culture folder (see <see cref="HelpContentLocator"/>).</item>
/// <item>An anonymous request gets <c>401</c> — not a redirect to a sign-in page — and a signed-in user who fails the
/// configured policy or roles gets <c>403</c>. The check runs before the file is looked up, so the answer does not
/// reveal which files exist. The endpoint is marked <c>AllowAnonymous</c> only so a fallback policy of the host does
/// not turn the 401 into a challenge; the endpoint enforces the sign-in itself.</item>
/// <item>Markdown is delivered without its <c>&lt;!-- Internal-Start --&gt;</c> … <c>&lt;!-- Internal-End --&gt;</c> blocks
/// (<see cref="HelpInternalContentFilter"/>) and with <c>Cache-Control: private, no-store</c>.</item>
/// </list>
/// Map it after <c>UseAuthentication</c>, <c>UseAuthorization</c> and any static-file middleware, and before a
/// fallback to <c>index.html</c>.
/// </remarks>
public static class HelpContentEndpointMapper
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    /// <summary>Maps the help content endpoint for the folder <paramref name="contentRoot"/>.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="contentRoot">The folder that holds one sub-folder per culture.</param>
    /// <param name="configure">Adjusts the remaining <see cref="HelpContentEndpointOptions"/>.</param>
    public static IEndpointRouteBuilder MapBlazorBaseHelpContent(this IEndpointRouteBuilder endpoints, string contentRoot, Action<HelpContentEndpointOptions>? configure = null) =>
        endpoints.MapBlazorBaseHelpContent(options =>
        {
            options.ContentRoot = contentRoot;
            configure?.Invoke(options);
        });

    /// <summary>Maps the help content endpoint described by <paramref name="configure"/>.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Sets the <see cref="HelpContentEndpointOptions"/>, at least <see cref="HelpContentEndpointOptions.ContentRoot"/>.</param>
    public static IEndpointRouteBuilder MapBlazorBaseHelpContent(this IEndpointRouteBuilder endpoints, Action<HelpContentEndpointOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new HelpContentEndpointOptions();
        configure(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ContentRoot, nameof(options.ContentRoot));

        var locator = new HelpContentLocator(options.ContentRoot, options.ExcludedFolders);
        var roles = SplitRoles(options.Roles);
        var requestPath = "/" + options.RequestPath.Trim('/');

        endpoints
            .MapGet(requestPath + "/{culture}/{**path}", (string culture, string? path, HttpContext httpContext) => ServeAsync(culture, path, httpContext, locator, options, roles))
            .AllowAnonymous();

        return endpoints;
    }

    private static async Task<IResult> ServeAsync(string culture, string? path, HttpContext httpContext, HelpContentLocator locator, HelpContentEndpointOptions options, string[] roles)
    {
        var user = httpContext.User;

        if (user.Identity?.IsAuthenticated != true)
            return Results.StatusCode(StatusCodes.Status401Unauthorized);

        if (roles.Length > 0 && !roles.Any(user.IsInRole))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        if (options.PolicyName is { Length: > 0 } policyName)
        {
            var authorization = httpContext.RequestServices.GetRequiredService<IAuthorizationService>();

            if (!(await authorization.AuthorizeAsync(user, policyName)).Succeeded)
                return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var file = locator.Locate(culture, path);

        if (file is null)
            return Results.NotFound();

        httpContext.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";

        if (file.IsMarkdown)
        {
            httpContext.Response.Headers[HeaderNames.CacheControl] = "private, no-store";
            var markdown = await File.ReadAllTextAsync(file.PhysicalPath, httpContext.RequestAborted);

            return Results.Text(HelpInternalContentFilter.Remove(markdown), file.ContentType, Utf8WithoutBom);
        }

        httpContext.Response.Headers[HeaderNames.CacheControl] = "private, no-cache";
        httpContext.Response.Headers[HeaderNames.ContentSecurityPolicy] = "default-src 'none'; style-src 'unsafe-inline'; sandbox";

        var info = new FileInfo(file.PhysicalPath);
        var lastModified = new DateTimeOffset(info.LastWriteTimeUtc);
        var tag = new EntityTagHeaderValue($"\"{lastModified.ToUnixTimeMilliseconds():x}-{info.Length:x}\"");

        return Results.File(file.PhysicalPath, file.ContentType, lastModified: lastModified, entityTag: tag);
    }

    private static string[] SplitRoles(string? roles) =>
        roles?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
}
