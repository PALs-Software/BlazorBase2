using BlazorBase.CRUD.Models;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.User.Server.Test.Services;

/// <summary>
/// A <see cref="UserModel"/> subclass adds a column to the admin user list: the provider fills it through
/// <c>MapToModelAsync</c> and sorts by it through <c>ApplySort</c>.
/// </summary>
public sealed class UserDataProviderExtensionTests : IdentityServerTestBase
{
    private sealed class LanguageUserModel : UserModel
    {
        public string Language { get; set; } = string.Empty;
    }

    private sealed class LanguageUserDataProvider(
        UserManager<TestUser> userManager,
        IHttpContextAccessor httpContextAccessor,
        TestUserDbContext dbContext)
        : UserDataProvider<TestUser, LanguageUserModel>(userManager, httpContextAccessor, dbContext)
    {
        protected override async Task<LanguageUserModel> MapToModelAsync(TestUser user)
        {
            var model = await base.MapToModelAsync(user);
            model.Language = user.Language;
            return model;
        }

        protected override IOrderedQueryable<TestUser>? ApplySort(IQueryable<TestUser> source, IOrderedQueryable<TestUser>? ordered, SortDescriptor sort)
        {
            if (sort.PropertyName != nameof(LanguageUserModel.Language))
                return ordered;

            if (ordered is null)
                return sort.Direction == SortDirection.Ascending ? source.OrderBy(user => user.Language) : source.OrderByDescending(user => user.Language);

            return sort.Direction == SortDirection.Ascending ? ordered.ThenBy(user => user.Language) : ordered.ThenByDescending(user => user.Language);
        }
    }

    [Fact]
    public async Task SeveralBuiltInSorts_ApplyInOrder_TheFirstIsPrimary()
    {
        await SeedAsync("z@example.com", "de", "A");
        await SeedAsync("y@example.com", "de", "A");
        await SeedAsync("x@example.com", "de", "B");

        using var scope = CreateScope();
        var provider = CreateProvider(scope);

        var byEmail = await provider.GetListAsync(Sorted((nameof(UserModel.Email), SortDirection.Ascending)));
        var byDisplayNameThenEmail = await provider.GetListAsync(Sorted(
            (nameof(UserModel.DisplayName), SortDirection.Ascending),
            (nameof(UserModel.Email), SortDirection.Ascending)));
        var byDisplayNameThenEmailDescending = await provider.GetListAsync(Sorted(
            (nameof(UserModel.DisplayName), SortDirection.Ascending),
            (nameof(UserModel.Email), SortDirection.Descending)));

        Assert.Equal(["x@example.com", "y@example.com", "z@example.com"], byEmail.Items.Select(item => item.Email));
        Assert.Equal(["y@example.com", "z@example.com", "x@example.com"], byDisplayNameThenEmail.Items.Select(item => item.Email));
        Assert.Equal(["z@example.com", "y@example.com", "x@example.com"], byDisplayNameThenEmailDescending.Items.Select(item => item.Email));
    }

    [Fact]
    public async Task ABuiltInSort_FollowedByTheSubclassColumn_GetsTheOrderedQueryInTheHook()
    {
        await SeedAsync("u1@example.com", "de", "A");
        await SeedAsync("u2@example.com", "en", "A");
        await SeedAsync("u3@example.com", "fr", "B");

        using var scope = CreateScope();
        var provider = CreateProvider(scope);

        var result = await provider.GetListAsync(Sorted(
            (nameof(UserModel.DisplayName), SortDirection.Ascending),
            (nameof(LanguageUserModel.Language), SortDirection.Descending)));

        Assert.Equal(["u2@example.com", "u1@example.com", "u3@example.com"], result.Items.Select(item => item.Email));
    }

    [Fact]
    public async Task TheSubclassColumn_FollowedByABuiltInSort_BreaksTiesWithTheBuiltInSort()
    {
        await SeedAsync("a@example.com", "de");
        await SeedAsync("b@example.com", "en");
        await SeedAsync("c@example.com", "de");

        using var scope = CreateScope();
        var provider = CreateProvider(scope);

        var result = await provider.GetListAsync(Sorted(
            (nameof(LanguageUserModel.Language), SortDirection.Ascending),
            (nameof(UserModel.Email), SortDirection.Descending)));

        Assert.Equal(["c@example.com", "a@example.com", "b@example.com"], result.Items.Select(item => item.Email));
    }

    [Fact]
    public async Task TheSubclassColumn_IsFilledAndSortable()
    {
        await SeedAsync("a@example.com", "de");
        await SeedAsync("b@example.com", "en");
        await SeedAsync("c@example.com", "de");

        using var scope = CreateScope();
        var provider = CreateProvider(scope);

        var ascending = await provider.GetListAsync(Sorted(SortDirection.Ascending));
        var descending = await provider.GetListAsync(Sorted(SortDirection.Descending));

        Assert.Equal(["de", "de", "en"], ascending.Items.Select(item => item.Language));
        Assert.Equal("en", descending.Items.First().Language);
    }

    [Fact]
    public async Task AnUnknownSort_IsIgnoredByTheDefaultHook()
    {
        await SeedAsync("a@example.com", "de");

        using var scope = CreateScope();
        var provider = new UserDataProvider<TestUser>(
            scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>(),
            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>(),
            scope.ServiceProvider.GetRequiredService<TestUserDbContext>());

        var result = await provider.GetListAsync(new BaseQuery { Sorts = [new SortDescriptor { PropertyName = "Unknown", Direction = SortDirection.Ascending }] });

        Assert.Single(result.Items);
    }

    private static BaseQuery Sorted(SortDirection direction)
        => Sorted((nameof(LanguageUserModel.Language), direction));

    private static BaseQuery Sorted(params (string PropertyName, SortDirection Direction)[] sorts)
        => new() { Sorts = [.. sorts.Select(sort => new SortDescriptor { PropertyName = sort.PropertyName, Direction = sort.Direction })] };

    private async Task SeedAsync(string email, string language, string? displayName = null)
    {
        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var result = await userManager.CreateAsync(new TestUser { UserName = email, Email = email, DisplayName = displayName ?? email, Language = language }, "Passw0rd!x");
        Assert.True(result.Succeeded);
    }

    private static LanguageUserDataProvider CreateProvider(IServiceScope scope)
        => new(
            scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>(),
            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>(),
            scope.ServiceProvider.GetRequiredService<TestUserDbContext>());
}
