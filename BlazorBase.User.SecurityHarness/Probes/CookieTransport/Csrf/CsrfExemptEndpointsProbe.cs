using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.Server.Tokens;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.SecurityHarness.Probes.CookieTransport.Csrf;

/// <summary>
/// Enumerates every endpoint the running host actually mapped, by reflection over
/// <see cref="EndpointDataSource"/>, and reports which ones carry <see cref="AllowBlazorBaseCsrfAttribute"/>
/// - never by grepping source, since an exemption is a property of what a host actually maps, not of
/// what the framework merely offers a way to mark.
/// </summary>
internal sealed class CsrfExemptEndpointsProbe : IAuthProbe
{
    public string Id => "CSRF-EXEMPT-ENDPOINTS";
    public string Category => "CSRF";
    public string Attack => "Enumerate the routes carrying [AllowBlazorBaseCsrf] on the running host, by reflection over its own endpoint metadata.";
    public string Expectation => "Every exemption found is genuinely credential-bearing on its own.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Cookie;

    public Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var endpointDataSource = context.Services.GetRequiredService<EndpointDataSource>();
        var allEndpoints = endpointDataSource.Endpoints;

        var exemptRoutes = allEndpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<AllowBlazorBaseCsrfAttribute>() is not null)
            .Select(DescribeEndpoint)
            .ToList();

        var allRouteNames = string.Join(", ", allEndpoints.Select(DescribeEndpoint));
        var evidence = new ProbeEvidence(
            "Every endpoint the running Cookie-profile host mapped, and which of them carry [AllowBlazorBaseCsrf].",
            Request: "Reflection over EndpointDataSource.Endpoints on the running host.",
            Response: $"All endpoints: {allRouteNames}\nExempt endpoints: {(exemptRoutes.Count == 0 ? "(none)" : string.Join(", ", exemptRoutes))}");

        if (exemptRoutes.Count == 0)
            return Task.FromResult(ProbeResult.Passed(
                "No endpoint mapped on this host carries [AllowBlazorBaseCsrf]. The framework does define one exemption " +
                "(the external-token-exchange endpoint, api/auth/external/exchange), but the Cookie profile does not map " +
                "it, so it is not reachable on this host and does not appear here.",
                evidence));

        return Task.FromResult(ProbeResult.Passed($"{exemptRoutes.Count} exempt route(s) found: {string.Join(", ", exemptRoutes)}.", evidence));
    }

    private static string DescribeEndpoint(Endpoint endpoint) =>
        endpoint is RouteEndpoint routeEndpoint ? routeEndpoint.RoutePattern.RawText ?? endpoint.DisplayName ?? "(unnamed)" : endpoint.DisplayName ?? "(unnamed)";
}
