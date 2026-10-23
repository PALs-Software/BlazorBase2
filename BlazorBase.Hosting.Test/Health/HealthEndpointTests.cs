using BlazorBase.Hosting.Health;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace BlazorBase.Hosting.Test.Health;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Endpoints_ReturnHealthy_WhenDatabaseIsReachable()
    {
        await using var host = await StartAsync(UseReachableDatabase);

        foreach (var path in new[] { "/health", "/health/live", "/health/ready" })
        {
            var response = await host.Client.GetAsync(path);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Ready_ReturnsServiceUnavailable_WhenDatabaseIsNotReachable_ButLiveStaysHealthy()
    {
        await using var host = await StartAsync(UseUnreachableDatabase);

        var ready = await host.Client.GetAsync("/health/ready");
        var aggregate = await host.Client.GetAsync("/health");
        var live = await host.Client.GetAsync("/health/live");

        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync());
        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, aggregate.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task Ready_IncludesReadinessChecksRegisteredByOtherProviders()
    {
        await using var host = await StartAsync(
            UseReachableDatabase,
            healthChecks => healthChecks.AddBlazorBaseReadinessCheck<FailingHealthCheck>("storage"));

        var ready = await host.Client.GetAsync("/health/ready");
        var live = await host.Client.GetAsync("/health/live");

        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task Endpoints_AreAnonymous_EvenWithAFallbackPolicyThatRequiresAuthentication()
    {
        await using var host = await StartAsync(
            UseReachableDatabase,
            configureServices: services => services.AddAuthorization(options =>
                options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));

        var response = await host.Client.GetAsync("/health");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Endpoints_AreNotCached()
    {
        await using var host = await StartAsync(UseReachableDatabase);

        var response = await host.Client.GetAsync("/health");

        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task CustomPattern_MapsLiveAndReadyBelowIt()
    {
        await using var host = await StartAsync(UseReachableDatabase, pattern: "/status/");

        Assert.Equal(System.Net.HttpStatusCode.OK, (await host.Client.GetAsync("/status")).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await host.Client.GetAsync("/status/live")).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await host.Client.GetAsync("/status/ready")).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await host.Client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task HostWithoutTheHelpers_ExposesNoHealthEndpoint()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        await app.StartAsync();

        var response = await app.GetTestClient().GetAsync("/health");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    private static void UseReachableDatabase(DbContextOptionsBuilder options) => options.UseSqlite("Data Source=:memory:");

    private static void UseUnreachableDatabase(DbContextOptionsBuilder options) => options.UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.db")};Mode=ReadWrite");

    private static async Task<HealthTestHost> StartAsync(
        Action<DbContextOptionsBuilder> configureDatabase,
        Action<IHealthChecksBuilder>? configureHealthChecks = null,
        Action<IServiceCollection>? configureServices = null,
        string pattern = "/health")
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContextFactory<HostingTestDbContext>(configureDatabase);
        builder.Services.AddAuthorization();
        var healthChecks = builder.Services.AddBlazorBaseHealthChecks<HostingTestDbContext>();
        configureHealthChecks?.Invoke(healthChecks);
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();
        app.UseAuthorization();
        app.MapBlazorBaseHealthChecks(pattern);
        await app.StartAsync();

        return new HealthTestHost(app);
    }

    private sealed class HealthTestHost(WebApplication app) : IAsyncDisposable
    {
        public HttpClient Client { get; } = app.GetTestClient();

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class FailingHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Unhealthy("Storage is down."));
    }
}
