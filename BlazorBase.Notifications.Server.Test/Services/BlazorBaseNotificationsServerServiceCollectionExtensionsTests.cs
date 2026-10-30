using System.Security.Claims;
using BlazorBase.Notifications.Server.Configuration;
using BlazorBase.Notifications.Server.Services;
using BlazorBase.Notifications.Server.Test.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.Notifications.Server.Test.Services;

public sealed class BlazorBaseNotificationsServerServiceCollectionExtensionsTests : IDisposable
{
    private readonly SqliteConnection Connection;

    public BlazorBaseNotificationsServerServiceCollectionExtensionsTests()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();
    }

    private static IConfiguration ConfigurationWith(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    private ServiceProvider BuildProvider(IConfiguration configuration, Action<WebPushOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TestNotificationsDbContext>(options => options.UseSqlite(Connection));
        services.AddBlazorBaseNotificationsServer<TestNotificationsDbContext>(configuration, configure: configure);
        return services.BuildServiceProvider();
    }

    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["BlazorBaseNotifications:WebPush:Subject"] = "mailto:support@example.test",
        ["BlazorBaseNotifications:WebPush:PublicKey"] = "test-public-key",
        ["BlazorBaseNotifications:WebPush:PrivateKey"] = "test-private-key",
    };

    [Fact]
    public void AddBlazorBaseNotificationsServer_BridgesBaseDbContext_FromConcreteHostContext()
    {
        using var provider = BuildProvider(ConfigurationWith(ValidSettings()));
        using var scope = provider.CreateScope();

        var concreteDbContext = scope.ServiceProvider.GetRequiredService<TestNotificationsDbContext>();
        var baseDbContext = scope.ServiceProvider.GetRequiredService<DbContext>();

        Assert.Same(concreteDbContext, baseDbContext);
    }

    [Fact]
    public void AddBlazorBaseNotificationsServer_RegistersThePushSubscriptionStore_AndTheNotifierPipeline()
    {
        using var provider = BuildProvider(ConfigurationWith(ValidSettings()));
        using var scope = provider.CreateScope();

        Assert.IsType<EfPushSubscriptionStore>(scope.ServiceProvider.GetRequiredService<IPushSubscriptionStore>());
        Assert.IsType<WebPushSender>(scope.ServiceProvider.GetRequiredService<IWebPushSender>());
        Assert.IsType<PushNotifier>(scope.ServiceProvider.GetRequiredService<IPushNotifier>());
    }

    [Fact]
    public void AddBlazorBaseNotificationsServer_RegistersTheClaimsUserIdResolver_ByDefault()
    {
        using var provider = BuildProvider(ConfigurationWith(ValidSettings()));
        using var scope = provider.CreateScope();

        Assert.IsType<ClaimsNotificationUserIdResolver>(scope.ServiceProvider.GetRequiredService<INotificationUserIdResolver>());
    }

    [Fact]
    public void AddBlazorBaseNotificationsServer_KeepsAHostRegisteredUserIdResolver()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TestNotificationsDbContext>(options => options.UseSqlite(Connection));
        services.AddScoped<INotificationUserIdResolver, FixedNotificationUserIdResolver>();
        services.AddBlazorBaseNotificationsServer<TestNotificationsDbContext>(ConfigurationWith(ValidSettings()));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<FixedNotificationUserIdResolver>(scope.ServiceProvider.GetRequiredService<INotificationUserIdResolver>());
    }

    private sealed class FixedNotificationUserIdResolver : INotificationUserIdResolver
    {
        public Task<Guid?> ResolveUserIdAsync(ClaimsPrincipal user, CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(Guid.Empty);
    }

    [Fact]
    public void AddBlazorBaseNotificationsServer_BindsWebPushOptions_FromConfiguration()
    {
        using var provider = BuildProvider(ConfigurationWith(ValidSettings()));

        var options = provider.GetRequiredService<IOptions<WebPushOptions>>().Value;

        Assert.Equal("mailto:support@example.test", options.Subject);
        Assert.Equal("test-public-key", options.PublicKey);
        Assert.Equal("test-private-key", options.PrivateKey);
    }

    [Fact]
    public void AddBlazorBaseNotificationsServer_LetsTheConfigureDelegate_OverrideBoundValues()
    {
        using var provider = BuildProvider(ConfigurationWith(ValidSettings()), configure: options => options.PublicKey = "overridden-public-key");

        var options = provider.GetRequiredService<IOptions<WebPushOptions>>().Value;

        Assert.Equal("overridden-public-key", options.PublicKey);
    }

    [Fact]
    public void AddBlazorBaseNotificationsServer_FailsFast_WhenThePublicKeyIsMissing()
    {
        var settings = ValidSettings();
        settings.Remove("BlazorBaseNotifications:WebPush:PublicKey");
        using var provider = BuildProvider(ConfigurationWith(settings));

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<WebPushOptions>>().Value);
    }

    [Fact]
    public void AddBlazorBaseNotificationsServer_FailsFast_WhenThePrivateKeyIsMissing()
    {
        var settings = ValidSettings();
        settings.Remove("BlazorBaseNotifications:WebPush:PrivateKey");
        using var provider = BuildProvider(ConfigurationWith(settings));

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<WebPushOptions>>().Value);
    }

    [Fact]
    public void AddBlazorBaseNotificationsServer_FailsFast_WhenTheSubjectIsMissing()
    {
        var settings = ValidSettings();
        settings.Remove("BlazorBaseNotifications:WebPush:Subject");
        using var provider = BuildProvider(ConfigurationWith(settings));

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<WebPushOptions>>().Value);
    }

    public void Dispose()
    {
        Connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
