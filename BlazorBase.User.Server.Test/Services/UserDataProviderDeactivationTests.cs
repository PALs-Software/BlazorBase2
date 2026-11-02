using BlazorBase.User.Models;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.User.Server.Test.Services;

/// <summary>
/// Deactivating a user through <see cref="UserDataProvider{TUser}"/> rotates the security stamp, so
/// the live bearer tokens, refresh tokens and cookie sessions of the account end at their next
/// stamp validation. Every other patch leaves the stamp alone.
/// </summary>
public sealed class UserDataProviderDeactivationTests : IdentityServerTestBase
{
    [Fact]
    public async Task Patch_DeactivatingAnActiveUser_RotatesTheSecurityStamp()
    {
        var user = await SeedUserAsync("active@example.com", "Active");
        var before = await ReadStampAsync(user.Id);

        using (var scope = CreateScope())
            await CreateProvider(scope).PatchAsync(user.Id, new Dictionary<string, object?> { [nameof(UserModel.IsActive)] = false });

        Assert.NotEqual(before, await ReadStampAsync(user.Id));
        Assert.False(await ReadIsActiveAsync(user.Id));
    }

    [Fact]
    public async Task Patch_ReactivatingOrChangingAnotherField_KeepsTheSecurityStamp()
    {
        var user = await SeedUserAsync("other@example.com", "Other");
        var before = await ReadStampAsync(user.Id);

        using (var scope = CreateScope())
            await CreateProvider(scope).PatchAsync(user.Id, new Dictionary<string, object?>
            {
                [nameof(UserModel.IsActive)] = true,
                [nameof(UserModel.DisplayName)] = "Renamed",
            });

        Assert.Equal(before, await ReadStampAsync(user.Id));
    }

    private static UserDataProvider<TestUser> CreateProvider(IServiceScope scope)
        => new(
            scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>(),
            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>(),
            scope.ServiceProvider.GetRequiredService<TestUserDbContext>(),
            []);

    private async Task<string?> ReadStampAsync(string userId)
    {
        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        return await userManager.GetSecurityStampAsync((await userManager.FindByIdAsync(userId))!);
    }

    private async Task<bool> ReadIsActiveAsync(string userId)
    {
        using var scope = CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>().FindByIdAsync(userId))!.IsActive;
    }
}
