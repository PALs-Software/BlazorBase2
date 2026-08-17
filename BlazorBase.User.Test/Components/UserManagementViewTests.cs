using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Core;
using BlazorBase.User.Models;
using BlazorBase.User.Pages;
using BlazorBase.User.Services;
using BlazorBase.User.Test.Infrastructure;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace BlazorBase.User.Test.Components;

/// <summary>
/// Proves the generic, framework-shipped <see cref="UserManagementView"/> renders the full user CRUD UI
/// over <see cref="BaseList{UserModel}"/>: it lists seeded users and offers create/edit through the card,
/// which relies on the registered password and role custom inputs.
/// </summary>
public class UserManagementViewTests : UserBunitTestContextBase
{
    private SeededUserModelDataProvider SetupServices(params UserModel[] users)
    {
        Services.AddLocalization();

        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("admin-user");
        authContext.SetRoles("Admin");

        Services.AddBlazorBaseUserManagementInputs();
        Services.AddSingleton<IUserRoleProvider>(new StubUserRoleProvider("Admin", "User"));

        var provider = new SeededUserModelDataProvider(users);
        Services.AddSingleton<IBaseDataProvider<UserModel>>(provider);

        return provider;
    }

    [Fact]
    public void UserManagementView_Renders_WithoutThrowing()
    {
        SetupServices();

        var cut = Render<UserManagementView>();

        Assert.Single(cut.FindComponents<BaseList<UserModel>>());
    }

    [Fact]
    public void UserManagementView_ShowsSeededUsers()
    {
        SetupServices(
            new UserModel { Id = "1", DisplayName = "Ada Lovelace", Email = "ada@example.com", Role = "Admin", IsActive = true },
            new UserModel { Id = "2", DisplayName = "Alan Turing", Email = "alan@example.com", Role = "User", IsActive = false });

        var cut = Render<UserManagementView>();

        var grid = cut.FindComponent<Microsoft.FluentUI.AspNetCore.Components.FluentDataGrid<UserModel>>();
        var items = grid.Instance.Items!.ToList();

        Assert.Equal(2, items.Count);
        Assert.Contains(items, user => user.DisplayName == "Ada Lovelace" && user.Email == "ada@example.com");
        Assert.Contains(items, user => user.DisplayName == "Alan Turing");
    }

    [Fact]
    public void UserManagementView_OffersCreate_ViaAddButton()
    {
        SetupServices();

        var cut = Render<UserManagementView>();
        var localizer = Services.GetRequiredService<IStringLocalizer<UserManagementView>>();

        Assert.Contains(localizer["AddUser"].Value, cut.Markup);
    }

    [Fact]
    public void UserCard_RendersUserModel_WithRegisteredInputs()
    {
        var provider = SetupServices(
            new UserModel { Id = "1", DisplayName = "Ada Lovelace", Email = "ada@example.com", Role = "Admin", IsActive = true });

        var editModel = provider.GetByIdAsync("1").GetAwaiter().GetResult()!;

        var card = Render<BlazorBase.User.Components.UserCard>(parameters => parameters
            .Add(p => p.Model, editModel));

        Assert.Single(card.FindComponents<BlazorBase.User.Components.UserManagementInputs.UserRoleInput>());
        Assert.Single(card.FindComponents<BlazorBase.User.Components.UserManagementInputs.UserPasswordInput>());

        var createCard = Render<BlazorBase.User.Components.UserCard>(parameters => parameters
            .Add(p => p.Model, new UserModel()));

        Assert.Single(createCard.FindComponents<BlazorBase.User.Components.UserManagementInputs.UserRoleInput>());
        Assert.Single(createCard.FindComponents<BlazorBase.User.Components.UserManagementInputs.UserPasswordInput>());
    }
}
