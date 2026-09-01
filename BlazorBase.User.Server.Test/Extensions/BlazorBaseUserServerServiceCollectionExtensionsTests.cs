using System.Collections;
using System.Reflection;
using BlazorBase.CRUD.Core;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Configuration;
using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
using BlazorBase.User.Server.Tokens;
using Microsoft.AspNetCore.Authentication;
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
    /// <summary>
    /// A host that dispatches with its own policy scheme sets <c>ClaimDefaultScheme = false</c>
    /// precisely so nothing else takes the default. Opting into cookies must respect that, or the
    /// host's scheme stops being consulted and every request authenticates against the wrong one.
    /// </summary>
    [Fact]
    public void AddBlazorBaseCookieAuthTokens_LeavesTheDefaultScheme_WhenTheHostKeptItsOwn()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = BuildConfiguration();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));
        services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration, options =>
        {
            options.SchemeName = "HostBearer";
            options.ClaimDefaultScheme = false;
        });
        services.AddBlazorBaseCookieAuthTokens(configuration);

        using var provider = services.BuildServiceProvider();
        var authenticationOptions = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        Assert.Null(authenticationOptions.DefaultAuthenticateScheme);
        Assert.Null(authenticationOptions.DefaultChallengeScheme);
    }

    [Fact]
    public void AddBlazorBaseCookieAuthTokens_ClaimsTheDefaultScheme_WhenTheHostDidNotOptOut()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = BuildConfiguration();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));
        services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration);
        services.AddBlazorBaseCookieAuthTokens(configuration);

        using var provider = services.BuildServiceProvider();
        var authenticationOptions = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        Assert.Equal(BlazorBaseAuthTokenTransportExtensions.BearerOrCookieScheme, authenticationOptions.DefaultAuthenticateScheme);
        Assert.Equal(BlazorBaseAuthTokenTransportExtensions.BearerOrCookieScheme, authenticationOptions.DefaultChallengeScheme);
    }

    [Fact]
    public void AddBlazorBaseUserServer_ResolvesUserModelDataProvider_WithBridgedDbContext()
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
        using var scope = provider.CreateScope();

        var dataProvider = scope.ServiceProvider.GetRequiredService<IBaseDataProvider<UserModel>>();
        var bridgedContext = scope.ServiceProvider.GetRequiredService<BaseUserDbContext<TestUser>>();

        Assert.NotNull(dataProvider);
        Assert.Same(scope.ServiceProvider.GetRequiredService<TestUserDbContext>(), bridgedContext);
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
            scope.ServiceProvider.GetRequiredService<SignInManager<TestUser>>(),
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
            scope.ServiceProvider.GetRequiredService<AuthTokenIssuer<TestUser>>(),
            scope.ServiceProvider.GetRequiredService<TwoFactorPendingTokenService<TestUser>>(),
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

    [Fact]
    public async Task AddBlazorBaseUserServer_RegistersDefaultBearerScheme_AsAuthenticationDefault_WhenNotConfigured()
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

        var authOptions = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        Assert.Equal(IdentityConstants.BearerScheme, authOptions.DefaultAuthenticateScheme);
        Assert.Equal(IdentityConstants.BearerScheme, authOptions.DefaultChallengeScheme);

        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = await schemeProvider.GetSchemeAsync(IdentityConstants.BearerScheme);
        Assert.NotNull(scheme);
    }

    [Fact]
    public async Task AddBlazorBaseUserServer_RegistersUnderCustomSchemeName_WithoutClaimingTheDefault_WhenConfigured()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = BuildConfiguration();
        const string customSchemeName = "BlazorBaseBearer";

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));
        services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration, options =>
        {
            options.SchemeName = customSchemeName;
            options.ClaimDefaultScheme = false;
        });

        using var provider = services.BuildServiceProvider();

        var authOptions = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        Assert.NotEqual(customSchemeName, authOptions.DefaultAuthenticateScheme);
        Assert.NotEqual(customSchemeName, authOptions.DefaultChallengeScheme);

        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = await schemeProvider.GetSchemeAsync(customSchemeName);
        Assert.NotNull(scheme);

        var defaultNamedScheme = await schemeProvider.GetSchemeAsync(IdentityConstants.BearerScheme);
        Assert.Null(defaultNamedScheme);
    }

    private static IConfiguration BuildConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthTokens:AccessTokenMinutes"] = "15",
                ["AuthTokens:RefreshTokenDays"] = "30",
            })
            .Build();
}
