using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorBase.User.SecurityHarness.IdentityProvider;

/// <summary>
/// A real Kestrel host that behaves like an OpenID Connect provider well enough for
/// <c>OidcExternalTokenValidator</c> to run against it completely unmodified: an actual discovery
/// document, an actual JWKS backed by <see cref="FakeSigningKeyRegistry"/>, and an availability
/// switch a probe can flip to observe what a cached configuration does once the provider goes
/// dark. Serves both a concrete tenant's issuer and, at the <c>common</c> tenant segment, the
/// tenant-templated issuer a real multi-tenant Entra authority publishes.
/// </summary>
public sealed class FakeIdentityProviderHost : IAsyncDisposable
{
    public required Uri BaseAddress { get; init; }
    public required FakeSigningKeyRegistry KeyRegistry { get; init; }
    private readonly WebApplication App;
    private readonly FakeIdentityProviderAvailability Availability;

    private FakeIdentityProviderHost(WebApplication app, FakeIdentityProviderAvailability availability)
    {
        App = app;
        Availability = availability;
    }

    /// <summary>The authority URL a concrete tenant publishes, e.g. <c>{BaseAddress}/{tenantId}/v2.0</c>.</summary>
    public string TenantAuthority(string tenantId) => $"{BaseAddress.ToString().TrimEnd('/')}/{tenantId}/v2.0";

    /// <summary>Takes the provider offline: discovery and JWKS requests both fail with <c>503</c>.</summary>
    public void GoOffline() => Availability.SetOnline(false);

    /// <summary>Brings a previously offline provider back without restarting its Kestrel host.</summary>
    public void GoOnline() => Availability.SetOnline(true);

    public async ValueTask DisposeAsync()
    {
        await App.StopAsync();
        await App.DisposeAsync();
        KeyRegistry.Dispose();
    }

    public static async Task<FakeIdentityProviderHost> StartAsync(X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        var keyRegistry = new FakeSigningKeyRegistry();
        var availability = new FakeIdentityProviderAvailability();
        string? baseAddressText = null;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            EnvironmentName = Environments.Production,
            ApplicationName = "BlazorBase.User.SecurityHarness.IdentityProvider",
        });

        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0, listenOptions =>
            listenOptions.UseHttps(certificate)));

        var app = builder.Build();

        app.MapGet("/{tenant}/v2.0/.well-known/openid-configuration", (string tenant) =>
        {
            if (!availability.IsOnline)
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            var baseAddress = baseAddressText!;
            var issuerTenant = tenant.Equals(FakeIdentityProviderTenants.MultiTenantSegment, StringComparison.OrdinalIgnoreCase)
                ? "{tenantid}"
                : tenant;

            return Results.Json(new
            {
                issuer = $"{baseAddress}/{issuerTenant}/v2.0",
                jwks_uri = $"{baseAddress}/{tenant}/discovery/v2.0/keys",
                authorization_endpoint = $"{baseAddress}/{tenant}/oauth2/v2.0/authorize",
                token_endpoint = $"{baseAddress}/{tenant}/oauth2/v2.0/token",
                response_types_supported = new[] { "code", "id_token", "code id_token" },
                id_token_signing_alg_values_supported = new[] { "RS256" },
            });
        });

        app.MapGet("/{tenant}/discovery/v2.0/keys", (string tenant) =>
            availability.IsOnline
                ? Results.Json(keyRegistry.BuildJwksDocument())
                : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

        await app.StartAsync(cancellationToken);

        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
        var address = addresses?.FirstOrDefault() ?? throw new InvalidOperationException("The fake identity provider reported no listening address.");
        baseAddressText = address.TrimEnd('/');

        return new FakeIdentityProviderHost(app, availability)
        {
            BaseAddress = new Uri(address),
            KeyRegistry = keyRegistry,
        };
    }
}
