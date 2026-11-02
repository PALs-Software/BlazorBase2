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
            => sort.PropertyName == nameof(LanguageUserModel.Language)
                ? sort.Direction == SortDirection.Ascending ? source.OrderBy(user => user.Language) : source.OrderByDescending(user => user.Language)
                : ordered;
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
        => new() { Sorts = [new SortDescriptor { PropertyName = nameof(LanguageUserModel.Language), Direction = direction }] };

    private async Task SeedAsync(string email, string language)
    {
        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var result = await userManager.CreateAsync(new TestUser { UserName = email, Email = email, DisplayName = email, Language = language }, "Passw0rd!x");
        Assert.True(result.Succeeded);
    }

    private static LanguageUserDataProvider CreateProvider(IServiceScope scope)
        => new(
            scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>(),
            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>(),
            scope.ServiceProvider.GetRequiredService<TestUserDbContext>());
}
