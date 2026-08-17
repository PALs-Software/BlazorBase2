using System.Security.Claims;
using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>
/// Spins up a real Identity stack (Identity Core + roles + EF stores) over an in-memory SQLite
/// database, wired exactly like <see cref="BlazorBaseUserServerServiceCollectionExtensions"/>, so the
/// server services and controllers can be exercised end-to-end. One instance per test (xUnit creates
/// a fresh instance per fact), giving each test an isolated database.
/// </summary>
public abstract class IdentityServerTestBase : IDisposable
{
    private readonly SqliteConnection Connection;

    protected ServiceProvider ServiceProvider { get; }
    protected IConfiguration Configuration { get; }

    /// <summary>
    /// Extra configuration entries a derived fixture needs, merged over the JWT defaults.
    /// </summary>
    protected virtual IDictionary<string, string?> AdditionalConfiguration => new Dictionary<string, string?>();

    /// <summary>
    /// Hook for a derived fixture to register services the feature under test needs.
    /// </summary>
    protected virtual void ConfigureAdditionalServices(IServiceCollection services)
    {
    }

    protected IdentityServerTestBase()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var configurationEntries = new Dictionary<string, string?>
        {
            ["JwtSettings:Secret"] = "blazorbase-test-signing-secret-key-0123456789-abcdef",
            ["JwtSettings:Issuer"] = "BlazorBase.User.Server.Test",
            ["JwtSettings:Audience"] = "BlazorBase.User.Server.Test.Audience",
            ["JwtSettings:ExpirationMinutes"] = "60",
            ["JwtSettings:RefreshExpirationDays"] = "30",
        };

        foreach (var entry in AdditionalConfiguration)
            configurationEntries[entry.Key] = entry.Value;

        Configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationEntries)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddSingleton(Configuration);
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

        services.AddScoped<TokenService<TestUser>>();

        // The same bridge AddBlazorBaseUserServer registers, so services that take the base
        // context resolve here as they do in a real host.
        services.AddScoped<BaseUserDbContext<TestUser>>(provider => provider.GetRequiredService<TestUserDbContext>());

        ConfigureAdditionalServices(services);

        ServiceProvider = services.BuildServiceProvider();

        using var scope = ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestUserDbContext>();
        context.Database.EnsureCreated();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        roleManager.CreateAsync(new IdentityRole("Admin")).GetAwaiter().GetResult();
        roleManager.CreateAsync(new IdentityRole("User")).GetAwaiter().GetResult();
    }

    protected IServiceScope CreateScope() => ServiceProvider.CreateScope();

    protected TestAuthController CreateAuthController(IServiceScope scope)
    {
        var provider = scope.ServiceProvider;
        var controller = new TestAuthController(
            provider.GetRequiredService<UserManager<TestUser>>(),
            provider.GetRequiredService<RoleManager<IdentityRole>>(),
            provider.GetRequiredService<TokenService<TestUser>>(),
            provider.GetRequiredService<TestUserDbContext>(),
            Configuration);

        // RequestServices, because the controller resolves optional services (the development
        // session, the logger) from the request rather than its constructor.
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = provider },
        };

        return controller;
    }

    protected TestUserController CreateUserController(IServiceScope scope, ClaimsPrincipal? principal = null)
    {
        var provider = scope.ServiceProvider;
        var controller = new TestUserController(provider.GetRequiredService<UserManager<TestUser>>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal ?? new ClaimsPrincipal(new ClaimsIdentity()) },
        };
        return controller;
    }

    protected async Task<TestUser> SeedUserAsync(
        string email,
        string displayName,
        string role = "User",
        bool isActive = true,
        string password = "Secret123")
    {
        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();

        var user = new TestUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            IsActive = isActive,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, role);
        return user;
    }

    protected void SetCurrentUser(string userId)
    {
        var accessor = ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test");
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    protected static ClaimsPrincipal PrincipalFor(string userId)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test"));

    public void Dispose()
    {
        ServiceProvider.Dispose();
        Connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
