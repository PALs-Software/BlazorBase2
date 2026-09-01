using BlazorBase.User.Server.Test.Infrastructure;
using BlazorBase.User.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.User.Server.Test.Extensions;

/// <summary>
/// Covers the length increase to 12 (#23) and that <c>AddBlazorBaseUserServer</c>'s
/// <c>configurePassword</c> delegate lets a host override the framework's own opinion regardless of
/// call order.
/// </summary>
public sealed class PasswordPolicyTests
{
    private static IConfiguration BuildConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthTokens:AccessTokenMinutes"] = "15",
                ["AuthTokens:RefreshTokenDays"] = "30",
            })
            .Build();

    private static async Task<ServiceProvider> BuildProviderAsync(Action<PasswordOptions>? configurePassword)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = BuildConfiguration();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));
        services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration, configurePassword: configurePassword);

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TestUserDbContext>().Database.EnsureCreatedAsync();

        return provider;
    }

    [Fact]
    public async Task An_11_character_password_is_rejected_by_default()
    {
        using var provider = await BuildProviderAsync(configurePassword: null);
        using var scope = provider.CreateScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = new TestUser { UserName = "eleven@example.com", Email = "eleven@example.com" };

        var result = await userManager.CreateAsync(user, "Elevn1Chars");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "PasswordTooShort");
    }

    [Fact]
    public async Task A_12_character_password_is_accepted_by_default()
    {
        using var provider = await BuildProviderAsync(configurePassword: null);
        using var scope = provider.CreateScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = new TestUser { UserName = "twelve@example.com", Email = "twelve@example.com" };

        var result = await userManager.CreateAsync(user, "Twelve1Chars");

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    [Fact]
    public async Task A_host_override_can_relax_the_minimum_length()
    {
        using var provider = await BuildProviderAsync(configurePassword: options => options.RequiredLength = 8);
        using var scope = provider.CreateScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = new TestUser { UserName = "eight@example.com", Email = "eight@example.com" };

        var result = await userManager.CreateAsync(user, "Elevn1Chars"[..8]);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
    }
}
