using BlazorBase.User.Server.Configuration;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.User.Server.Test.Extensions;

/// <summary>
/// The registration is the outermost guard: a host that ships with the option still switched on
/// must fail to start rather than come up with a passwordless door.
/// </summary>
public class DevelopmentAuthenticationRegistrationTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("QA")]
    public void AddBlazorBaseDevelopmentAuthentication_Throws_WhenEnabledOutsideDevelopment(string environmentName)
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddBlazorBaseDevelopmentAuthentication<TestUser>(
                BuildConfiguration(enabled: true),
                new StubHostEnvironment(environmentName)));

        Assert.Contains(environmentName, exception.Message);
        Assert.Contains(DevelopmentAuthenticationOptions.SectionName, exception.Message);
    }

    [Fact]
    public void AddBlazorBaseDevelopmentAuthentication_Accepts_WhenEnabledInDevelopment()
    {
        var services = new ServiceCollection();

        services.AddBlazorBaseDevelopmentAuthentication<TestUser>(
            BuildConfiguration(enabled: true),
            new StubHostEnvironment(Environments.Development));

        Assert.Contains(services, service => service.ServiceType == typeof(IDevelopmentSessionService<TestUser>));
    }

    /// <summary>
    /// Safe to call unconditionally: with the option absent or off, production startup is unaffected.
    /// </summary>
    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void AddBlazorBaseDevelopmentAuthentication_IsHarmless_WhenDisabled(string environmentName)
    {
        var services = new ServiceCollection();

        services.AddBlazorBaseDevelopmentAuthentication<TestUser>(
            BuildConfiguration(enabled: false),
            new StubHostEnvironment(environmentName));

        Assert.Contains(services, service => service.ServiceType == typeof(IDevelopmentSessionService<TestUser>));
    }

    [Fact]
    public void AddBlazorBaseDevelopmentAuthentication_IsHarmless_WhenTheSectionIsMissing()
    {
        var services = new ServiceCollection();
        var emptyConfiguration = new ConfigurationBuilder().Build();

        services.AddBlazorBaseDevelopmentAuthentication<TestUser>(
            emptyConfiguration,
            new StubHostEnvironment("Production"));

        Assert.Contains(services, service => service.ServiceType == typeof(IDevelopmentSessionService<TestUser>));
    }

    /// <summary>
    /// Bound through the real configuration pipeline, because the binder <em>appends</em> to a
    /// collection that already holds items: a default of <c>["Admin"]</c> on the property would
    /// turn a configured <c>["User"]</c> into <c>["Admin", "User"]</c> and hand out admin rights
    /// to a developer who asked for the opposite. Setting the options object directly in a test
    /// would never catch that.
    /// </summary>
    [Fact]
    public void Roles_ConfiguredAsUser_DoNotPickUpTheAdminDefault()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DevelopmentAuthenticationOptions.SectionName}:Enabled"] = "true",
                [$"{DevelopmentAuthenticationOptions.SectionName}:Roles:0"] = "User",
            })
            .Build();

        services.AddBlazorBaseDevelopmentAuthentication<TestUser>(
            configuration,
            new StubHostEnvironment(Environments.Development));

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<DevelopmentAuthenticationOptions>>().Value;

        Assert.Equal(["User"], options.EffectiveRoles);
    }

    [Fact]
    public void Roles_LeftOutOfTheConfiguration_FallBackToAdmin()
    {
        var services = new ServiceCollection();

        services.AddBlazorBaseDevelopmentAuthentication<TestUser>(
            BuildConfiguration(enabled: true),
            new StubHostEnvironment(Environments.Development));

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<DevelopmentAuthenticationOptions>>().Value;

        Assert.Equal(DevelopmentAuthenticationOptions.DefaultRoles, options.EffectiveRoles);
    }

    [Fact]
    public void Roles_ConfiguredWithSeveralEntries_AreAllKept()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DevelopmentAuthenticationOptions.SectionName}:Enabled"] = "true",
                [$"{DevelopmentAuthenticationOptions.SectionName}:Roles:0"] = "User",
                [$"{DevelopmentAuthenticationOptions.SectionName}:Roles:1"] = "Reviewer",
            })
            .Build();

        services.AddBlazorBaseDevelopmentAuthentication<TestUser>(
            configuration,
            new StubHostEnvironment(Environments.Development));

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<DevelopmentAuthenticationOptions>>().Value;

        Assert.Equal(["User", "Reviewer"], options.EffectiveRoles);
    }

    private static IConfiguration BuildConfiguration(bool enabled)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DevelopmentAuthenticationOptions.SectionName}:Enabled"] = enabled ? "true" : "false",
            })
            .Build();
}
