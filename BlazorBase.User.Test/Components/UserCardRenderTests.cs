using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Extensions;
using BlazorBase.User.Components.UserManagementInputs;
using BlazorBase.User.Services;
using BlazorBase.User.Test.Infrastructure;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Xunit;

namespace BlazorBase.User.Test.Components;

/// <summary>
/// The key proof for foundation (B): the generic <see cref="BaseCard{TModel}"/> renders the user
/// model cleanly once the custom password and role inputs are registered. A prior attempt to render
/// the user model in the generic card/list threw a Fluent UI <c>FieldIdentifier</c> "index
/// expression" error because the bound value expression was not a clean member access. The custom
/// inputs bind a local string with plain <c>@bind-Value</c>, which keeps Fluent UI's
/// <c>FieldIdentifier.Create</c> happy.
/// </summary>
public class UserCardRenderTests : UserBunitTestContextBase
{
    [Fact]
    public void BaseCard_RendersUserModel_WithRegisteredInputs_WithoutThrowing()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("admin-user");
        authContext.SetRoles("Admin");

        Services.AddBlazorBaseUserManagementInputs();
        Services.AddBlazorBaseUserRoleProvider<RolesForCard>();

        var model = new CardTestUser
        {
            Id = "1",
            DisplayName = "Ada Lovelace",
            Email = "ada@example.com",
            Role = "Admin",
            IsActive = true,
        };

        var configuration = new BaseCardBuilder<CardTestUser>()
            .Field(u => u.DisplayName, f => f.Label("Display Name"))
            .Field(u => u.Email, f => f.Label("Email"))
            .Field(u => u.Role, f => f.Label("Role"))
            .Field(u => u.IsActive, f => f.Label("Active"))
            .Field(u => u.Password, f => f.Label("Password"))
            .Build();

        var cut = Render<BaseCard<CardTestUser>>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.Configuration, configuration));

        Assert.Single(cut.FindComponents<UserRoleInput>());
        Assert.Single(cut.FindComponents<UserPasswordInput>());
        Assert.Equal(TextFieldType.Password, cut.FindComponent<UserPasswordInput>().FindComponent<FluentTextField>().Instance.TextFieldType);
        Assert.Contains("Admin", cut.Markup);
    }

    private sealed class RolesForCard : IUserRoleProvider
    {
        public IReadOnlyList<string> GetRoleNames() => ["Admin", "User"];
    }
}
