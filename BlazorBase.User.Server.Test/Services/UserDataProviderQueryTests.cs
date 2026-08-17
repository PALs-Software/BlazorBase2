using BlazorBase.CRUD.Models;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.User.Server.Test.Services;

/// <summary>
/// Covers the query surface of <see cref="UserDataProvider{TUser}"/> — filtering, sorting, paging,
/// counting, email-rename side effects, and the "cannot delete your own account" guard — complementing
/// the create/patch round-trip already covered in <c>BlazorBase.User.Test</c>.
/// </summary>
public sealed class UserDataProviderQueryTests : IdentityServerTestBase
{
    [Fact]
    public async Task GetList_FiltersByDisplayName_UsingContains()
    {
        await SeedUserAsync("alice@example.com", "Alice");
        await SeedUserAsync("bob@example.com", "Bob");
        await SeedUserAsync("alicia@example.com", "Alicia");

        var result = await QueryAsync(new BaseQuery
        {
            Filters = [new FilterDescriptor { PropertyName = nameof(UserModel.DisplayName), Operator = FilterOperator.Contains, Value = "Ali" }],
        });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Contains("Ali", item.DisplayName));
    }

    [Fact]
    public async Task GetList_FiltersByIsActive_UsingEquals()
    {
        await SeedUserAsync("active@example.com", "Active User", isActive: true);
        await SeedUserAsync("inactive@example.com", "Inactive User", isActive: false);

        var result = await QueryAsync(new BaseQuery
        {
            Filters = [new FilterDescriptor { PropertyName = nameof(UserModel.IsActive), Operator = FilterOperator.Equals, Value = false }],
        });

        var single = Assert.Single(result.Items);
        Assert.False(single.IsActive);
        Assert.Equal("inactive@example.com", single.Email);
    }

    [Fact]
    public async Task GetList_SortsByEmailDescending()
    {
        await SeedUserAsync("a@example.com", "A");
        await SeedUserAsync("c@example.com", "C");
        await SeedUserAsync("b@example.com", "B");

        var result = await QueryAsync(new BaseQuery
        {
            Sorts = [new SortDescriptor { PropertyName = nameof(UserModel.Email), Direction = SortDirection.Descending }],
        });

        Assert.Equal(["c@example.com", "b@example.com", "a@example.com"], result.Items.Select(item => item.Email));
    }

    [Fact]
    public async Task GetList_AppliesPaging_AndReportsFullTotalCount()
    {
        foreach (var name in new[] { "User A", "User B", "User C", "User D", "User E" })
            await SeedUserAsync($"{name.Replace(" ", "").ToLowerInvariant()}@example.com", name);

        var result = await QueryAsync(new BaseQuery
        {
            Sorts = [new SortDescriptor { PropertyName = nameof(UserModel.DisplayName), Direction = SortDirection.Ascending }],
            Skip = 2,
            Take = 2,
        });

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(["User C", "User D"], result.Items.Select(item => item.DisplayName));
    }

    [Fact]
    public async Task GetCount_ReturnsTotalUserCount()
    {
        await SeedUserAsync("one@example.com", "One");
        await SeedUserAsync("two@example.com", "Two");

        using var scope = CreateScope();
        var provider = CreateProvider(scope);

        Assert.Equal(2, await provider.GetCountAsync());
    }

    [Fact]
    public async Task Patch_RenamingEmail_AlsoUpdatesUserName()
    {
        var user = await SeedUserAsync("old@example.com", "Renamed");

        using (var patchScope = CreateScope())
        {
            var provider = CreateProvider(patchScope);
            await provider.PatchAsync(user.Id, new Dictionary<string, object?>
            {
                [nameof(UserModel.Email)] = "new@example.com",
            });
        }

        using var verifyScope = CreateScope();
        var userManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var reloaded = await userManager.FindByIdAsync(user.Id);

        Assert.Equal("new@example.com", reloaded!.Email);
        Assert.Equal("new@example.com", reloaded.UserName);
    }

    [Fact]
    public async Task Delete_RemovesTheUser()
    {
        var keep = await SeedUserAsync("keep@example.com", "Keep");
        var remove = await SeedUserAsync("remove@example.com", "Remove");

        using (var deleteScope = CreateScope())
            await CreateProvider(deleteScope).DeleteAsync(remove.Id);

        using var verifyScope = CreateScope();
        var provider = CreateProvider(verifyScope);
        Assert.Null(await provider.GetByIdAsync(remove.Id));
        Assert.NotNull(await provider.GetByIdAsync(keep.Id));
    }

    [Fact]
    public async Task Delete_ThrowsWhenDeletingOwnAccount()
    {
        var user = await SeedUserAsync("self@example.com", "Self");
        SetCurrentUser(user.Id);

        using var scope = CreateScope();
        var provider = CreateProvider(scope);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.DeleteAsync(user.Id));
    }

    /// <summary>
    /// A missing password is something the administrator can correct, so it has to arrive as a
    /// validation message rather than as an unhandled exception the client sees as a 500.
    /// </summary>
    [Fact]
    public async Task Create_ReportsAValidationError_WhenThePasswordIsMissing()
    {
        using var scope = CreateScope();
        var provider = CreateProvider(scope);

        await Assert.ThrowsAsync<BaseValidationException>(() => provider.CreateAsync(new UserModel
        {
            DisplayName = "No Password",
            Email = "nopassword@example.com",
            Role = "User",
            IsActive = true,
            Password = null,
        }));
    }

    /// <summary>
    /// The roles offered to an administrator come from the app's role provider while the rows are
    /// seeded elsewhere, so the two can disagree. Assigning a role that does not exist threw inside
    /// Identity <em>after</em> the account had been written, leaving a user with no role behind.
    /// </summary>
    [Fact]
    public async Task Create_ReportsAValidationError_AndWritesNothing_WhenTheRoleDoesNotExist()
    {
        using var scope = CreateScope();
        var provider = CreateProvider(scope);

        await Assert.ThrowsAsync<BaseValidationException>(() => provider.CreateAsync(new UserModel
        {
            DisplayName = "Unknown Role",
            Email = "unknownrole@example.com",
            Role = "Moderator",
            IsActive = true,
            Password = "Str0ngPassword!",
        }));

        var users = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        Assert.Null(await users.FindByEmailAsync("unknownrole@example.com"));
    }

    [Fact]
    public async Task Create_ReportsAValidationError_WhenNoRoleIsChosen()
    {
        using var scope = CreateScope();
        var provider = CreateProvider(scope);

        await Assert.ThrowsAsync<BaseValidationException>(() => provider.CreateAsync(new UserModel
        {
            DisplayName = "No Role",
            Email = "norole@example.com",
            Role = string.Empty,
            IsActive = true,
            Password = "Str0ngPassword!",
        }));
    }

    [Fact]
    public async Task Patch_ResettingPassword_RevokesAllActiveRefreshTokens()
    {
        var user = await SeedUserAsync("reset@example.com", "Reset");

        using (var seedScope = CreateScope())
        {
            var context = seedScope.ServiceProvider.GetRequiredService<TestUserDbContext>();
            context.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = "seeded-token-hash",
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
            });
            await context.SaveChangesAsync();
        }

        using (var patchScope = CreateScope())
        {
            var provider = CreateProvider(patchScope);
            await provider.PatchAsync(user.Id, new Dictionary<string, object?>
            {
                [nameof(UserModel.Password)] = "NewSecret123",
            });
        }

        using var verifyScope = CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TestUserDbContext>();
        var tokens = await verifyContext.RefreshTokens.Where(r => r.UserId == user.Id).ToListAsync();

        Assert.NotEmpty(tokens);
        Assert.All(tokens, token => Assert.True(token.IsRevoked));
    }

    private async Task<BaseQueryResult<UserModel>> QueryAsync(BaseQuery query)
    {
        using var scope = CreateScope();
        return await CreateProvider(scope).GetListAsync(query);
    }

    private static UserDataProvider<TestUser> CreateProvider(IServiceScope scope)
        => new(
            scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>(),
            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>(),
            scope.ServiceProvider.GetRequiredService<TestUserDbContext>());
}
