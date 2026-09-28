using System.Collections.Concurrent;
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
/// <remarks>
/// Also serves a real authorization endpoint and a real token endpoint, so an
/// <c>AddOpenIdConnect</c> handler registered by <see cref="Hosting.HarnessHostFactory"/> can run
/// the full browser-redirect authorization-code flow against this provider unmodified, the same way
/// <see cref="OidcExternalTokenValidator"/> already does for the token-exchange flow. The
/// authorization endpoint auto-approves every request as <see cref="RedirectFlowSubject"/> - there
/// is no interactive login page - and mints a single-use authorization code the token endpoint
/// exchanges for a freshly RS256-signed id_token.
/// </remarks>
public sealed class FakeIdentityProviderHost : IAsyncDisposable
{
    /// <summary>The subject every authorization-code-flow id_token this provider mints carries.</summary>
    public const string RedirectFlowSubject = "harness-redirect-subject@harness.local";

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
        var authorizationCodes = new ConcurrentDictionary<string, AuthorizationCodeEntry>();
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

        app.MapGet("/{tenant}/oauth2/v2.0/authorize", (string tenant, HttpRequest request) =>
        {
            if (!availability.IsOnline)
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            var redirectUri = request.Query["redirect_uri"].ToString();

            if (string.IsNullOrEmpty(redirectUri))
                return Results.BadRequest(new { error = "invalid_request", error_description = "Missing redirect_uri." });

            var code = Guid.NewGuid().ToString("N");
            authorizationCodes[code] = new AuthorizationCodeEntry(
                tenant,
                request.Query["client_id"].ToString(),
                request.Query["nonce"].ToString() is { Length: > 0 } nonce ? nonce : null,
                DateTimeOffset.UtcNow.AddMinutes(2));

            var state = request.Query["state"].ToString();
            var separator = redirectUri.Contains('?', StringComparison.Ordinal) ? '&' : '?';
            var location = $"{redirectUri}{separator}code={Uri.EscapeDataString(code)}&state={Uri.EscapeDataString(state)}";

            return Results.Redirect(location);
        });

        app.MapPost("/{tenant}/oauth2/v2.0/token", async (HttpRequest request) =>
        {
            if (!availability.IsOnline)
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            var form = await request.ReadFormAsync();
            var code = form["code"].ToString();

            if (string.IsNullOrEmpty(code) || !authorizationCodes.TryRemove(code, out var entry) || entry.ExpiresAt < DateTimeOffset.UtcNow)
                return Results.BadRequest(new { error = "invalid_grant", error_description = "Unknown, already-used or expired authorization code." });

            var extraClaims = entry.Nonce is null
                ? null
                : new Dictionary<string, object> { ["nonce"] = entry.Nonce };

            var description = new FakeTokenDescription
            {
                Issuer = $"{baseAddressText}/{entry.Tenant}/v2.0",
                Audience = entry.ClientId,
                Subject = RedirectFlowSubject,
                TenantId = entry.Tenant,
                ObjectId = RedirectFlowSubject,
                IssuedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                NotBefore = DateTimeOffset.UtcNow.AddMinutes(-1),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
                ExtraClaims = extraClaims,
            };

            var idToken = FakeTokenBuilder.CreateRsaSignedToken(description, FakeSigningKeyRegistry.KeyIdA, keyRegistry.GetKey(FakeSigningKeyRegistry.KeyIdA));

            return Results.Json(new
            {
                token_type = "Bearer",
                id_token = idToken,
                access_token = Guid.NewGuid().ToString("N"),
                expires_in = 300,
            });
        });

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

/// <summary>
/// A single-use authorization code minted by the authorize endpoint, redeemed by the token endpoint
/// - the state the fake provider needs to carry between the two, since it never persists anything
/// beyond one run.
/// </summary>
internal sealed record AuthorizationCodeEntry(string Tenant, string ClientId, string? Nonce, DateTimeOffset ExpiresAt);

