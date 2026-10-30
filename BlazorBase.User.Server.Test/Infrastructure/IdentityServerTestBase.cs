using System.Security.Claims;
using BlazorBase.User.Server.Configuration;
using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Security;
using BlazorBase.User.Server.Services;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>
/// Spins up a real Identity stack (Identity Core + roles + EF stores) over an in-memory SQLite
/// database, wired exactly like <see cref="BlazorBaseUserServerServiceCollectionExtensions"/>, so the
/// server services and controllers can be exercised end-to-end. One instance per test (xUnit creates
/// a fresh instance per fact), giving each test an isolated database. Also registers the
/// <see cref="IdentityConstants.ApplicationScheme"/> cookie handler - not wired by
/// <c>AddBlazorBaseUserServer</c> itself, but required for <c>SignInManager.RefreshSignInAsync</c> to
/// have somewhere to write the reissued cookie, exactly as the real host wires it via
/// <c>AddBlazorBaseCookieAuthTokens</c>.
/// </summary>
public abstract class IdentityServerTestBase : IDisposable
{
    private readonly SqliteConnection Connection;

    protected ServiceProvider ServiceProvider { get; }
    protected IConfiguration Configuration { get; }

    /// <summary>
    /// Extra configuration entries a derived fixture needs, merged over the auth-token defaults.
    /// </summary>
    protected virtual IDictionary<string, string?> AdditionalConfiguration => new Dictionary<string, string?>();

    /// <summary>
    /// Hook for a derived fixture to register services the feature under test needs.
    /// </summary>
    protected virtual void ConfigureAdditionalServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// Hook for a derived fixture to adjust the <see cref="TestUserDbContext"/> options, for example to
    /// swap the execution strategy.
    /// </summary>
    protected virtual void ConfigureDbContextOptions(DbContextOptionsBuilder options)
    {
    }

    /// <summary>
    /// A test-controllable clock, so refresh-token expiry can be proven without a real wall-clock
    /// wait. Exposed for a derived fixture to advance.
    /// </summary>
    protected ManualTimeProvider TimeProvider { get; } = new();

    protected IdentityServerTestBase()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var configurationEntries = new Dictionary<string, string?>
        {
            ["AuthTokens:AccessTokenMinutes"] = "60",
            ["AuthTokens:RefreshTokenDays"] = "30",
        };

        foreach (var entry in AdditionalConfiguration)
            configurationEntries[entry.Key] = entry.Value;

        Configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationEntries)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddAuthentication()
            .AddCookie(IdentityConstants.ApplicationScheme)
            .AddBearerToken(IdentityConstants.BearerScheme, options =>
        {
            options.BearerTokenExpiration = TimeSpan.FromMinutes(Configuration.GetValue("AuthTokens:AccessTokenMinutes", 60));
            options.RefreshTokenExpiration = TimeSpan.FromDays(Configuration.GetValue("AuthTokens:RefreshTokenDays", 30));
        });
        services.AddSingleton<TimeProvider>(TimeProvider);
        services.AddSingleton(new BlazorBaseUserServerOptions());
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddSingleton(Configuration);
        services.AddDbContext<TestUserDbContext>(options =>
        {
            options.UseSqlite(Connection);
            ConfigureDbContextOptions(options);
        });

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
            .AddDefaultTokenProviders()
            .AddSignInManager()
            .AddClaimsPrincipalFactory<BlazorBaseUserClaimsPrincipalFactory<TestUser>>();

        services.AddScoped<AuthTokenIssuer<TestUser>>();
        services.AddSingleton<ITwoFactorPendingTokenStore, InMemoryTwoFactorPendingTokenStore>();
        services.AddSingleton(typeof(TwoFactorPendingTokenService<>));
        services.AddSingleton(new AccessTokenStampValidationOptions());
        services.AddSingleton<AccessTokenStampValidationCache>();
        services.AddScoped<IAccessTokenStampValidator, AccessTokenStampValidator<TestUser>>();

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
            provider.GetRequiredService<SignInManager<TestUser>>(),
            provider.GetRequiredService<RoleManager<IdentityRole>>(),
            provider.GetRequiredService<AuthTokenIssuer<TestUser>>(),
            provider.GetRequiredService<TwoFactorPendingTokenService<TestUser>>());

        // RequestServices, because the controller resolves optional services (the development
        // session, the logger) from the request rather than its constructor.
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = provider },
        };

        return controller;
    }

    /// <summary>
    /// <see cref="RequestServices"/> is set because <c>UpdateSettings</c> calls
    /// <c>SignInManager.RefreshSignInAsync</c>, which resolves <c>IAuthenticationService</c> (and the
    /// registered cookie handler) from it. The singleton <see cref="IHttpContextAccessor"/> is pointed
    /// at the same context too, because <c>SignInManager</c> reads its own context from the accessor,
    /// not from <see cref="ControllerBase.HttpContext"/>.
    /// </summary>
    protected TestUserController CreateUserController(IServiceScope scope, ClaimsPrincipal? principal = null)
    {
        var provider = scope.ServiceProvider;
        var controller = new TestUserController(
            provider.GetRequiredService<UserManager<TestUser>>(),
            provider.GetRequiredService<SignInManager<TestUser>>());

        var httpContext = new DefaultHttpContext
        {
            User = principal ?? new ClaimsPrincipal(new ClaimsIdentity()),
            RequestServices = provider,
        };

        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    protected async Task<TestUser> SeedUserAsync(
        string email,
        string displayName,
        string role = "User",
        bool isActive = true,
        string password = "Secret123456")
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

    /// <summary>
    /// Unprotects an access token minted by <see cref="AuthTokenIssuer"/>, so a test can assert on
    /// the claims it carries without a JWT library - the token is an opaque, data-protected blob.
    /// </summary>
    protected ClaimsPrincipal UnprotectAccessToken(string accessToken)
    {
        using var scope = CreateScope();
        var bearerTokenOptions = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<BearerTokenOptions>>()
            .Get(IdentityConstants.BearerScheme);

        return bearerTokenOptions.BearerTokenProtector.Unprotect(accessToken)!.Principal;
    }

    public void Dispose()
    {
        ServiceProvider.Dispose();
        Connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
