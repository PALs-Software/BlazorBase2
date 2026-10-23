using BlazorBase.Files.Server.Services;
using BlazorBase.Hosting.BackgroundJobs;
using BlazorBase.Mailing.Services;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BlazorBase.Hosting.Test.BackgroundJobs;

public sealed class BackgroundJobGateTests
{
    [Fact]
    public void Gate_IsEnabled_ByDefault()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>());

        Assert.True(provider.GetRequiredService<IBackgroundJobGate>().IsEnabled);
    }

    [Theory]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public void Gate_FollowsTheEnabledSetting(string value, bool expected)
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { ["BackgroundJobs:Enabled"] = value });

        Assert.Equal(expected, provider.GetRequiredService<IBackgroundJobGate>().IsEnabled);
    }

    [Fact]
    public void Gate_ReadsACustomSection()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jobs:Enabled"] = "false" }).Build();
        services.AddBlazorBaseBackgroundJobGate(configuration, "Jobs");
        using var provider = services.BuildServiceProvider();

        Assert.False(provider.GetRequiredService<IBackgroundJobGate>().IsEnabled);
    }

    [Fact]
    public async Task GatedHostedService_Starts_WhenEnabled()
    {
        var host = BuildHost(enabled: true, services => services.AddGatedHostedService<CountingHostedService>());

        await host.StartAsync();
        await host.StopAsync();

        Assert.Equal(1, CountingHostedService.Started);
        Assert.Equal(1, CountingHostedService.Stopped);
        host.Dispose();
    }

    [Fact]
    public async Task GatedHostedService_DoesNotStart_WhenDisabled()
    {
        var host = BuildHost(enabled: false, services => services.AddGatedHostedService<CountingHostedService>());

        await host.StartAsync();
        await host.StopAsync();

        Assert.Equal(0, CountingHostedService.Started);
        Assert.Equal(0, CountingHostedService.Stopped);
        host.Dispose();
    }

    [Fact]
    public async Task HostedServices_OfAHostWithoutTheGate_RunAsBefore()
    {
        CountingHostedService.Reset();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddHostedService<CountingHostedService>();
        var host = builder.Build();

        await host.StartAsync();
        await host.StopAsync();

        Assert.Equal(1, CountingHostedService.Started);
        host.Dispose();
    }

    [Fact]
    public void Gate_WrapsTheRecurringFrameworkHostedServices_ButNotTheEmailSender()
    {
        var services = new ServiceCollection();
        services.AddHostedService<BackgroundEmailSenderService>();
        services.AddHostedService<TemporaryFileCleanupService>();
        services.AddHostedService<RefreshTokenCleanupService<BaseUser>>();
        services.AddHostedService<CountingHostedService>();

        services.AddBlazorBaseBackgroundJobGate(new ConfigurationBuilder().Build());

        var implementationTypes = services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)).Select(descriptor => descriptor.ImplementationType).ToList();
        Assert.Equal(4, implementationTypes.Count);
        Assert.Contains(typeof(BackgroundEmailSenderService), implementationTypes);
        Assert.Contains(typeof(CountingHostedService), implementationTypes);
        Assert.DoesNotContain(typeof(TemporaryFileCleanupService), implementationTypes);
        Assert.DoesNotContain(typeof(RefreshTokenCleanupService<BaseUser>), implementationTypes);
    }

    [Fact]
    public async Task Gate_DoesNotStartTheWrappedFrameworkHostedServices_WhenDisabled()
    {
        var host = BuildHost(enabled: false, services =>
        {
            services.AddHostedService<TemporaryFileCleanupService>();
            services.AddHostedService<RefreshTokenCleanupService<BaseUser>>();
            services.AddHostedService<CountingHostedService>();
        });

        await host.StartAsync();
        await host.StopAsync();

        Assert.Equal(1, CountingHostedService.Started);
        host.Dispose();
    }

    [Fact]
    public void GatedTypeNames_MatchTheFrameworkTypes()
    {
        Assert.Contains(typeof(TemporaryFileCleanupService).FullName, BlazorBaseBackgroundJobsServiceCollectionExtensions.GatedFrameworkHostedServiceTypeNames);
        Assert.Contains(typeof(RefreshTokenCleanupService<>).FullName, BlazorBaseBackgroundJobsServiceCollectionExtensions.GatedFrameworkHostedServiceTypeNames);
        Assert.Equal(2, BlazorBaseBackgroundJobsServiceCollectionExtensions.GatedFrameworkHostedServiceTypeNames.Count);
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        services.AddBlazorBaseBackgroundJobGate(configuration);
        return services.BuildServiceProvider();
    }

    private static IHost BuildHost(bool enabled, Action<IServiceCollection> configureServices)
    {
        CountingHostedService.Reset();

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["BackgroundJobs:Enabled"] = enabled ? "true" : "false" });
        configureServices(builder.Services);
        builder.Services.AddBlazorBaseBackgroundJobGate(builder.Configuration);
        return builder.Build();
    }

    private sealed class CountingHostedService : IHostedService
    {
        public static int Started { get; private set; }

        public static int Stopped { get; private set; }

        public static void Reset()
        {
            Started = 0;
            Stopped = 0;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Started++;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stopped++;
            return Task.CompletedTask;
        }
    }
}
