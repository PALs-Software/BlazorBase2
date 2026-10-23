using Azure.Monitor.OpenTelemetry.AspNetCore;
using BlazorBase.Hosting.AzureMonitor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using Xunit;

namespace BlazorBase.Hosting.Test.AzureMonitor;

public sealed class AzureMonitorTests
{
    private const string ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://localhost/";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoConnectionString_RegistersNothing(string? value)
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(BlazorBaseAzureMonitorServiceCollectionExtensions.DefaultConnectionStringKey, value);

        var result = services.AddBlazorBaseAzureMonitor(configuration);

        Assert.Same(services, result);
        Assert.Empty(services);
    }

    [Fact]
    public void ConnectionStringUnderAnotherKey_IsIgnored_WhenTheConfiguredKeyIsMissing()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(BlazorBaseAzureMonitorServiceCollectionExtensions.DefaultConnectionStringKey, ConnectionString);

        services.AddBlazorBaseAzureMonitor(configuration, "Custom:ConnectionString");

        Assert.Empty(services);
    }

    [Fact]
    public void ConnectionString_EnablesAzureMonitorOpenTelemetry()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(BlazorBaseAzureMonitorServiceCollectionExtensions.DefaultConnectionStringKey, ConnectionString);

        services.AddBlazorBaseAzureMonitor(configuration);

        Assert.NotEmpty(services);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(TracerProvider));
        using var provider = services.BuildServiceProvider();
        Assert.Equal(ConnectionString, provider.GetRequiredService<IOptions<AzureMonitorOptions>>().Value.ConnectionString);
    }

    [Fact]
    public void CustomKeyAndConfigure_AreHonored()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration("Custom:ConnectionString", ConnectionString);

        services.AddBlazorBaseAzureMonitor(configuration, "Custom:ConnectionString", options => options.SamplingRatio = 0.25f);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AzureMonitorOptions>>().Value;
        Assert.Equal(ConnectionString, options.ConnectionString);
        Assert.Equal(0.25f, options.SamplingRatio);
    }

    private static IConfiguration CreateConfiguration(string key, string? value) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [key] = value }).Build();
}
