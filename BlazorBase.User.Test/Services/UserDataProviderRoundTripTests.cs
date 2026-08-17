using BlazorBase.User.Models;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Test.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.User.Test.Services;

/// <summary>
/// Proves the <see cref="IBaseUser"/> model round-trips through <see cref="UserDataProvider{TUser}"/>
/// against a real (in-memory SQLite) Identity store: create, read back, patch role + reset password.
/// </summary>
public sealed class UserDataProviderRoundTripTests : IDisposable
{
    private readonly SqliteConnection Connection;
    private readonly ServiceProvider ServiceProvider;

    public UserDataProviderRoundTripTests()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(Connection));

        services.AddIdentityCore<TestUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TestUserDbContext>()
            .AddDefaultTokenProviders();

        ServiceProvider = services.BuildServiceProvider();

        using var scope = ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestUserDbContext>();
        context.Database.EnsureCreated();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        roleManager.CreateAsync(new IdentityRole("Admin")).GetAwaiter().GetResult();
        roleManager.CreateAsync(new IdentityRole("User")).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Create_ThenReadBack_MapsBaseUserFields()
    {
        var provider = CreateProvider();

        var created = await provider.CreateAsync(new UserModel
        {
            DisplayName = "Ada Lovelace",
            Email = "ada@example.com",
            Role = "Admin",
            IsActive = true,
            Password = "Secret123",
        });

        Assert.False(string.IsNullOrEmpty(created.Id));
        Assert.Equal("Admin", created.Role);
        Assert.Null(created.Password);

        var readBack = await provider.GetByIdAsync(created.Id);

        Assert.NotNull(readBack);
        Assert.Equal("Ada Lovelace", readBack!.DisplayName);
        Assert.Equal("ada@example.com", readBack.Email);
        Assert.Equal("Admin", readBack.Role);
        Assert.True(readBack.IsActive);
        Assert.Null(readBack.Password);
    }

    [Fact]
    public async Task Patch_ChangesRole_AndResetsPasswordOnlyWhenProvided()
    {
        var provider = CreateProvider();

        var created = await provider.CreateAsync(new UserModel
        {
            DisplayName = "Grace Hopper",
            Email = "grace@example.com",
            Role = "User",
            IsActive = true,
            Password = "Secret123",
        });

        var patched = await provider.PatchAsync(created.Id, new Dictionary<string, object?>
        {
            [nameof(UserModel.Role)] = "Admin",
            [nameof(UserModel.Password)] = "NewSecret456",
        });

        Assert.Equal("Admin", patched.Role);

        var blankPasswordPatch = await provider.PatchAsync(created.Id, new Dictionary<string, object?>
        {
            [nameof(UserModel.DisplayName)] = "Grace M. Hopper",
            [nameof(UserModel.Password)] = "",
        });

        Assert.Equal("Grace M. Hopper", blankPasswordPatch.DisplayName);
        Assert.Equal("Admin", blankPasswordPatch.Role);
    }

    private UserDataProvider<TestUser> CreateProvider()
    {
        var scope = ServiceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestUserDbContext>();
        return new UserDataProvider<TestUser>(userManager, httpContextAccessor, dbContext);
    }

    public void Dispose()
    {
        ServiceProvider.Dispose();
        Connection.Dispose();
    }
}
