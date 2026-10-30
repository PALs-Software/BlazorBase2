using BlazorBase.CRUD.Components.CustomProperties;
using BlazorBase.User.Components.UserManagementInputs;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using BlazorBase.User.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Xunit;

namespace BlazorBase.User.Test.Components;

public class UserRoleInputTests : UserBunitTestContextBase
{
    [Fact]
    public void RoleInput_RendersOneOptionPerInjectedRole()
    {
        Services.AddSingleton<IUserRoleProvider>(new StubUserRoleProvider("Admin", "User", "Viewer"));

        var cut = RenderDirect(new RoleTestUser { Role = "User" });

        var options = OptionsOf(cut);
        Assert.Equal(["Admin", "User", "Viewer"], options.Select(option => option.Text));
    }

    [Fact]
    public async Task RoleInput_RoundTripsSelectionThroughValueChanged()
    {
        Services.AddSingleton<IUserRoleProvider>(new StubUserRoleProvider("Admin", "User"));

        object? emitted = null;
        var cut = RenderDirect(new RoleTestUser { Role = "User" }, value => emitted = value);

        var select = cut.FindComponent<FluentSelect<UserRoleInput.RoleOption, string>>().Instance;
        await cut.InvokeAsync(() => select.ValueChanged.InvokeAsync("Admin"));

        Assert.Equal("Admin", emitted);
    }

    [Fact]
    public void RoleInput_WithoutProvider_RendersNoOptions()
    {
        var cut = RenderDirect(new RoleTestUser());

        Assert.Empty(OptionsOf(cut));
    }

    /// <summary>
    /// A new user arrives with an empty role. Without an empty option the select showed its first
    /// role as though it had been chosen while the model stayed empty, so saving failed with
    /// "the Role field is required" under a field that looked filled in.
    /// </summary>
    [Fact]
    public void RoleInput_OffersAnEmptyOption_WhenNoRoleIsSelectedYet()
    {
        Services.AddSingleton<IUserRoleProvider>(new StubUserRoleProvider("Admin", "User"));

        var cut = RenderDirect(new RoleTestUser { Role = string.Empty });

        var options = OptionsOf(cut);
        Assert.Equal(3, options.Count);
        Assert.Equal(string.Empty, options[0].Value);
    }

    [Fact]
    public void RoleInput_OffersNoEmptyOption_WhenARoleIsAlreadySelected()
    {
        Services.AddSingleton<IUserRoleProvider>(new StubUserRoleProvider("Admin", "User"));

        var cut = RenderDirect(new RoleTestUser { Role = "User" });

        var options = OptionsOf(cut);
        Assert.Equal(2, options.Count);
        Assert.DoesNotContain(options, option => option.Value == string.Empty);
    }

    /// <summary>
    /// A role the provider no longer offers must not be hidden behind a role that was never chosen.
    /// </summary>
    [Fact]
    public void RoleInput_OffersAnEmptyOption_WhenTheStoredRoleIsUnknown()
    {
        Services.AddSingleton<IUserRoleProvider>(new StubUserRoleProvider("Admin", "User"));

        var cut = RenderDirect(new RoleTestUser { Role = "Moderator" });

        Assert.Equal(3, OptionsOf(cut).Count);
    }

    [Fact]
    public void RoleInput_SelectsTheEmptyOption_WhenTheStoredRoleIsUnknown()
    {
        Services.AddSingleton<IUserRoleProvider>(new StubUserRoleProvider("Admin", "User"));

        var cut = RenderDirect(new RoleTestUser { Role = "Moderator" });

        var select = cut.FindComponent<FluentSelect<UserRoleInput.RoleOption, string>>().Instance;
        Assert.Equal(string.Empty, select.Value);
    }

    [Fact]
    public void RoleInput_SelectsTheStoredRole_WhenItIsOffered()
    {
        Services.AddSingleton<IUserRoleProvider>(new StubUserRoleProvider("Admin", "User"));

        var cut = RenderDirect(new RoleTestUser { Role = "User" });

        var select = cut.FindComponent<FluentSelect<UserRoleInput.RoleOption, string>>().Instance;
        Assert.Equal("User", select.Value);
    }

    private static List<UserRoleInput.RoleOption> OptionsOf(IRenderedComponent<UserRoleInput> cut) =>
        [.. cut.FindComponent<FluentSelect<UserRoleInput.RoleOption, string>>().Instance.Items!];

    [Fact]
    public void RoleInput_CanHandle_MatchesRolePropertyByName()
    {
        var input = new UserRoleInput();

        var context = new CustomPropertyContext(
            typeof(RoleTestUser),
            typeof(UserModel).GetProperty(nameof(UserModel.Role))!,
            typeof(string),
            true);

        Assert.True(input.CanHandle(context));
    }

    private IRenderedComponent<UserRoleInput> RenderDirect(
        RoleTestUser model,
        Action<object?>? onValueChanged = null)
    {
        var property = typeof(UserModel).GetProperty(nameof(UserModel.Role))!;

        return Render<UserRoleInput>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.Property, property)
            .Add(p => p.Value, property.GetValue(model))
            .Add(p => p.IsEditing, true)
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<object?>(this, value => onValueChanged?.Invoke(value))));
    }
}
