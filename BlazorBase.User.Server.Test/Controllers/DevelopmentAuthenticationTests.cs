using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Configuration;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BlazorBase.User.Server.Test.Controllers;

/// <summary>
/// Development authentication signs a developer in without a login form. Because it hands out a
/// real token for a real account, the tests that matter are the ones proving it cannot be reached
/// from anywhere but a Development host — and that switching the configured roles actually
/// switches what the account can do.
/// </summary>
public sealed class DevelopmentAuthenticationTests : IdentityServerTestBase
{
    private const string DevelopmentEmail = "developer@localhost";

    private readonly StubHostEnvironment Environment = new(Environments.Development);

    private readonly DevelopmentAuthenticationOptions Options = new()
    {
        Enabled = true,
        Email = DevelopmentEmail,
        DisplayName = "Development User",
        Roles = ["Admin"]
    };

    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        services.AddSingleton<IHostEnvironment>(Environment);
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(Options));
        services.AddScoped<IDevelopmentSessionService<TestUser>, DevelopmentSessionService<TestUser>>();
    }

    [Fact]
    public async Task DevelopmentLogin_IssuesTokensForANewlyProvisionedAccount()
    {
        using var scope = CreateScope();
        var result = await CreateAuthController(scope).DevelopmentLogin(CancellationToken.None);

        var login = AssertOkLogin(result);
        Assert.False(string.IsNullOrEmpty(login.AccessToken));
        Assert.False(string.IsNullOrEmpty(login.RefreshToken));

        using var verifyScope = CreateScope();
        var userManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = await userManager.FindByEmailAsync(DevelopmentEmail);

        Assert.NotNull(user);
        Assert.Equal("Development User", user!.DisplayName);
    }

    [Fact]
    public async Task DevelopmentLogin_PutsTheConfiguredRolesIntoTheToken()
    {
        Options.Roles = ["Admin", "User"];

        using var scope = CreateScope();
        var result = await CreateAuthController(scope).DevelopmentLogin(CancellationToken.None);

        var roles = RolesInToken(AssertOkLogin(result).AccessToken);

        Assert.Contains("Admin", roles);
        Assert.Contains("User", roles);
    }

    /// <summary>
    /// The point of the feature is trying a feature as a different role, which only works if the
    /// roles shrink as well as grow.
    /// </summary>
    [Fact]
    public async Task DevelopmentLogin_RemovesRolesThatLeftTheConfiguration()
    {
        using (var firstScope = CreateScope())
            await CreateAuthController(firstScope).DevelopmentLogin(CancellationToken.None);

        Options.Roles = ["User"];

        using var secondScope = CreateScope();
        var result = await CreateAuthController(secondScope).DevelopmentLogin(CancellationToken.None);

        var roles = RolesInToken(AssertOkLogin(result).AccessToken);

        Assert.Contains("User", roles);
        Assert.DoesNotContain("Admin", roles);
    }

    [Fact]
    public async Task DevelopmentLogin_ReusesTheExistingAccountInsteadOfCreatingASecond()
    {
        using (var firstScope = CreateScope())
            await CreateAuthController(firstScope).DevelopmentLogin(CancellationToken.None);

        using (var secondScope = CreateScope())
            await CreateAuthController(secondScope).DevelopmentLogin(CancellationToken.None);

        using var verifyScope = CreateScope();
        var userManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();

        Assert.Single(userManager.Users.Where(candidate => candidate.Email == DevelopmentEmail));
    }

    /// <summary>
    /// A token is issued on every app start — every reload, every restart — so without cleanup the
    /// table would collect a row per reload and reach the hundreds over a day of development.
    /// </summary>
    [Fact]
    public async Task DevelopmentLogin_LeavesExactlyOneRefreshToken_HoweverOftenItIsCalled()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var scope = CreateScope();
            await CreateAuthController(scope).DevelopmentLogin(CancellationToken.None);
        }

        using var verifyScope = CreateScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<TestUserDbContext>();

        Assert.Single(dbContext.RefreshTokens);
    }

    [Fact]
    public async Task DevelopmentLogin_LeavesAnotherUsersRefreshTokensAlone()
    {
        var otherUser = await SeedUserAsync("someone.else@example.com", "Someone Else");

        using (var loginScope = CreateScope())
        {
            var login = await CreateAuthController(loginScope).Login(new LoginRequest
            {
                Email = "someone.else@example.com",
                Password = "Secret123",
            });

            Assert.IsType<OkObjectResult>(login.Result);
        }

        using (var developmentScope = CreateScope())
            await CreateAuthController(developmentScope).DevelopmentLogin(CancellationToken.None);

        using var verifyScope = CreateScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<TestUserDbContext>();

        Assert.Contains(dbContext.RefreshTokens, token => token.UserId == otherUser.Id);
    }

    [Fact]
    public async Task DevelopmentLogin_IsNotFound_WhenTheOptionIsOff()
    {
        Options.Enabled = false;

        using var scope = CreateScope();
        var result = await CreateAuthController(scope).DevelopmentLogin(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    /// <summary>
    /// The second gate: even with the option set, a non-development host must not expose the route.
    /// </summary>
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task DevelopmentLogin_IsNotFound_OutsideDevelopment(string environmentName)
    {
        Environment.EnvironmentName = environmentName;

        using var scope = CreateScope();
        var result = await CreateAuthController(scope).DevelopmentLogin(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);

        using var verifyScope = CreateScope();
        var userManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        Assert.Null(await userManager.FindByEmailAsync(DevelopmentEmail));
    }

    [Fact]
    public async Task Status_AnnouncesDevelopmentAuthentication_SoTheClientKnowsToUseIt()
    {
        using var scope = CreateScope();
        var result = await CreateAuthController(scope).GetStatus();

        var status = Assert.IsType<AuthStatusResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(status.DevelopmentAuthenticationEnabled);
    }

    [Fact]
    public async Task Status_KeepsQuietAboutIt_OutsideDevelopment()
    {
        Environment.EnvironmentName = "Production";

        using var scope = CreateScope();
        var result = await CreateAuthController(scope).GetStatus();

        var status = Assert.IsType<AuthStatusResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.False(status.DevelopmentAuthenticationEnabled);
    }

    [Fact]
    public async Task DevelopmentLogin_GivesTheAccountAPasswordNobodyHolds()
    {
        using var scope = CreateScope();
        await CreateAuthController(scope).DevelopmentLogin(CancellationToken.None);

        using var verifyScope = CreateScope();
        var userManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = await userManager.FindByEmailAsync(DevelopmentEmail);

        Assert.False(await userManager.CheckPasswordAsync(user!, "Secret123"));
        Assert.False(await userManager.CheckPasswordAsync(user!, DevelopmentEmail));
        Assert.False(await userManager.CheckPasswordAsync(user!, "developer"));
    }

    private static IReadOnlyList<string> RolesInToken(string accessToken)
        => new JwtSecurityTokenHandler()
            .ReadJwtToken(accessToken)
            .Claims
            .Where(claim => claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToList();

    private static LoginResponse AssertOkLogin(ActionResult<LoginResponse> result)
    {
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<LoginResponse>(okResult.Value);
    }
}
