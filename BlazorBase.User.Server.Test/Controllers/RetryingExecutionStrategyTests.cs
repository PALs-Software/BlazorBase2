using BlazorBase.User.Models;
using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.User.Server.Test.Controllers;

/// <summary>
/// Proves the explicit transactions of the auth stack (first-user setup, refresh-token rotation) stay
/// valid when the database provider uses a retrying execution strategy, as <c>EnableRetryOnFailure()</c>
/// does for Azure SQL: such a strategy rejects any transaction not wrapped in
/// <c>CreateExecutionStrategy().ExecuteAsync(...)</c> and re-runs the wrapped unit after a transient failure.
/// </summary>
public sealed class RetryingExecutionStrategyTests : IdentityServerTestBase
{
    private readonly TransientFailureInterceptor Interceptor = new();

    protected override void ConfigureDbContextOptions(DbContextOptionsBuilder options)
    {
        options.ReplaceService<IExecutionStrategyFactory, RetryingTestExecutionStrategyFactory>();
        options.AddInterceptors(Interceptor);
    }

    [Fact]
    public async Task Harness_RejectsATransactionOutsideAnExecutionStrategy()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestUserDbContext>();

        Assert.True(dbContext.Database.CreateExecutionStrategy().RetriesOnFailure);
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.Users.CountAsync());
    }

    [Fact]
    public async Task Setup_CreatesFirstAdmin_UnderRetryingStrategy()
    {
        var result = await SetupAsync();

        AssertOkLogin(result);
        Assert.Equal(1, await CountAsync(context => context.Users.CountAsync()));
        Assert.Equal(1, await CountAsync(context => context.SetupCompletionMarkers.CountAsync()));
    }

    [Fact]
    public async Task Setup_RetriesTransientFailure_AndCreatesExactlyOneAdmin()
    {
        Interceptor.FailuresRemaining = 1;

        var result = await SetupAsync();

        AssertOkLogin(result);
        Assert.Equal(0, Interceptor.FailuresRemaining);
        Assert.Equal(1, await CountAsync(context => context.Users.CountAsync()));
        Assert.Equal(1, await CountAsync(context => context.SetupCompletionMarkers.CountAsync()));
    }

    [Fact]
    public async Task Setup_ReturnsConflict_WhenMarkerAlreadyExists_UnderRetryingStrategy()
    {
        using (var scope = CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TestUserDbContext>();
            dbContext.SetupCompletionMarkers.Add(new SetupCompletionMarker());
            await dbContext.SaveChangesAsync();
        }

        var result = await SetupAsync();

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(0, await CountAsync(context => context.Users.CountAsync()));
    }

    [Fact]
    public async Task Setup_ReturnsBadRequest_WhenUserCannotBeCreated_UnderRetryingStrategy()
    {
        using var scope = CreateScope();

        var result = await CreateAuthController(scope).Setup(new SetupRequest { Email = "admin@example.com", DisplayName = "Admin", Password = "short" });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, await CountAsync(context => context.Users.CountAsync()));
        Assert.Equal(0, await CountAsync(context => context.SetupCompletionMarkers.CountAsync()));
    }

    [Fact]
    public async Task Rotate_RevokesAndIssues_UnderRetryingStrategy()
    {
        var (stored, principal) = await IssueInitialTokenAsync();

        var rotated = await RotateAsync(stored, principal);

        Assert.NotNull(rotated);
        Assert.Equal(2, await CountAsync(context => context.RefreshTokens.CountAsync()));
        Assert.Equal(1, await CountAsync(context => context.RefreshTokens.CountAsync(token => token.IsRevoked)));
    }

    [Fact]
    public async Task Rotate_RetriesTransientFailure_AndIssuesExactlyOneReplacement()
    {
        var (stored, principal) = await IssueInitialTokenAsync();
        Interceptor.FailuresRemaining = 1;

        var rotated = await RotateAsync(stored, principal);

        Assert.NotNull(rotated);
        Assert.Equal(0, Interceptor.FailuresRemaining);
        Assert.Equal(2, await CountAsync(context => context.RefreshTokens.CountAsync()));
        Assert.Equal(1, await CountAsync(context => context.RefreshTokens.CountAsync(token => token.IsRevoked)));
    }

    [Fact]
    public async Task Rotate_ReturnsNull_WhenTokenWasAlreadyRevoked_UnderRetryingStrategy()
    {
        var (stored, principal) = await IssueInitialTokenAsync();
        Assert.NotNull(await RotateAsync(stored, principal));

        var replay = await RotateAsync(stored, principal);

        Assert.Null(replay);
        Assert.Equal(2, await CountAsync(context => context.RefreshTokens.CountAsync()));
    }

    private async Task<ActionResult<LoginResponse>> SetupAsync()
    {
        using var scope = CreateScope();
        return await CreateAuthController(scope).Setup(new SetupRequest { Email = "admin@example.com", DisplayName = "Admin", Password = "Secret123456" });
    }

    private async Task<(StoredRefreshToken Stored, System.Security.Claims.ClaimsPrincipal Principal)> IssueInitialTokenAsync()
    {
        var user = await SeedUserAsync("ada@example.com", "Ada");

        using var scope = CreateScope();
        var provider = scope.ServiceProvider;
        var signInManager = provider.GetRequiredService<SignInManager<TestUser>>();
        var tokenIssuer = provider.GetRequiredService<AuthTokenIssuer<TestUser>>();

        var principal = await signInManager.CreateUserPrincipalAsync((await provider.GetRequiredService<UserManager<TestUser>>().FindByIdAsync(user.Id))!);
        var tokens = await tokenIssuer.IssueTokenPairAsync(principal);
        var stored = await tokenIssuer.FindAsync(tokens.RefreshToken);

        return (stored!, principal);
    }

    private async Task<LoginResponse?> RotateAsync(StoredRefreshToken stored, System.Security.Claims.ClaimsPrincipal principal)
    {
        using var scope = CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AuthTokenIssuer<TestUser>>().RotateAsync(stored, principal);
    }

    private async Task<int> CountAsync(Func<TestUserDbContext, Task<int>> count)
    {
        using var scope = CreateScope();
        return await count(scope.ServiceProvider.GetRequiredService<TestUserDbContext>());
    }

    private static LoginResponse AssertOkLogin(ActionResult<LoginResponse> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<LoginResponse>(ok.Value);
    }
}
