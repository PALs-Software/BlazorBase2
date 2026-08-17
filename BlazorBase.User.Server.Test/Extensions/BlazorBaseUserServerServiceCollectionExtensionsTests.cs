using System.Collections;
using System.Reflection;
using BlazorBase.CRUD.Core;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.User.Server.Test.Extensions;

public class BlazorBaseUserServerServiceCollectionExtensionsTests
{
    [Fact]
    public void AddBlazorBaseUserServer_ResolvesUserModelDataProvider_WithBridgedDbContext()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "blazorbase-test-signing-secret-key-0123456789-abcdef",
                ["JwtSettings:Issuer"] = "BlazorBase.User.Server.Test",
                ["JwtSettings:Audience"] = "BlazorBase.User.Server.Test.Audience",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));
        services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var dataProvider = scope.ServiceProvider.GetRequiredService<IBaseDataProvider<UserModel>>();
        var bridgedContext = scope.ServiceProvider.GetRequiredService<BaseUserDbContext<TestUser>>();

        Assert.NotNull(dataProvider);
        Assert.Same(scope.ServiceProvider.GetRequiredService<TestUserDbContext>(), bridgedContext);
    }

    [Fact]
    public void AddBlazorBaseUserServer_Throws_WhenSecretIsShorterThan32Bytes()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = BuildConfiguration(secret: "too-short-secret");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration));

        Assert.Contains("32 bytes", exception.Message);
    }

    [Fact]
    public void AddBlazorBaseUserServer_DoesNotThrow_WhenSecretIsAtLeast32Bytes()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = BuildConfiguration();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));

        var exception = Record.Exception(
            () => services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration));

        Assert.Null(exception);
    }

    [Fact]
    public void AddBlazorBaseUserServer_RegistersBlazorBaseAuthRateLimiterPolicy()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = BuildConfiguration();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));
        services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration);

        using var provider = services.BuildServiceProvider();

        var rateLimiterOptions = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;
        var policyMapProperty = typeof(RateLimiterOptions).GetProperty("PolicyMap", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var policyMap = Assert.IsAssignableFrom<IDictionary>(policyMapProperty.GetValue(rateLimiterOptions));

        Assert.True(policyMap.Contains("BlazorBaseAuth"));
    }

    [Fact]
    public async Task AddBlazorBaseUserServer_ProducesLoginResponse_WithDefault15MinuteExpiration_WhenNotConfigured()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = BuildConfiguration();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton(configuration);
        services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));
        services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<TestUserDbContext>();
        await context.Database.EnsureCreatedAsync();

        var controller = new TestAuthController(
            scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>(),
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
            scope.ServiceProvider.GetRequiredService<TokenService<TestUser>>(),
            scope.ServiceProvider.GetRequiredService<BaseUserDbContext<TestUser>>(),
            configuration)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var before = DateTime.UtcNow;
        var result = await controller.Setup(new SetupRequest
        {
            Email = "admin@example.com",
            DisplayName = "Admin",
            Password = "Secret123",
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var login = Assert.IsType<LoginResponse>(ok.Value);

        Assert.InRange(login.ExpiresAt, before.AddMinutes(14), before.AddMinutes(16));
    }

    private static IConfiguration BuildConfiguration(string secret = "blazorbase-test-signing-secret-key-0123456789-abcdef")
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = secret,
                ["JwtSettings:Issuer"] = "BlazorBase.User.Server.Test",
                ["JwtSettings:Audience"] = "BlazorBase.User.Server.Test.Audience",
            })
            .Build();
}
