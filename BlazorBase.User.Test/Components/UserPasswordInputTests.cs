using System.Linq.Expressions;
using System.Reflection;
using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Extensions;
using BlazorBase.User.Components.UserManagementInputs;
using BlazorBase.User.Models;
using BlazorBase.User.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Xunit;

namespace BlazorBase.User.Test.Components;

public class UserPasswordInputTests : UserBunitTestContextBase
{
    [Fact]
    public void PasswordInput_RendersForPasswordPropertyThroughGenericCard()
    {
        Services.AddBlazorBaseCustomInput<UserPasswordInput>();

        var cut = RenderViaCard(new PasswordTestUser(), Field(u => u.Password));

        Assert.Single(cut.FindComponents<UserPasswordInput>());
        Assert.Equal(TextFieldType.Password, cut.FindComponent<FluentTextField>().Instance.TextFieldType);
    }

    [Fact]
    public void PasswordInput_IsWriteOnly_StartsBlankEvenWhenModelHasValue()
    {
        var cut = RenderDirect(new PasswordTestUser { Password = "ExistingSecret1!" });

        Assert.True(string.IsNullOrEmpty(cut.FindComponent<FluentTextField>().Instance.Value));
    }

    [Fact]
    public async Task PasswordInput_BlankEntry_EmitsNullSoExistingPasswordIsKept()
    {
        object? emitted = "sentinel";
        var cut = RenderDirect(new PasswordTestUser { Password = "Untouched" }, value => emitted = value);

        var textField = cut.FindComponent<FluentTextField>().Instance;
        await cut.InvokeAsync(() => textField.ValueChanged.InvokeAsync(""));

        Assert.Null(emitted);
    }

    [Fact]
    public async Task PasswordInput_NonBlankEntry_EmitsTheNewValue()
    {
        object? emitted = null;
        var cut = RenderDirect(new PasswordTestUser(), value => emitted = value);

        var textField = cut.FindComponent<FluentTextField>().Instance;
        await cut.InvokeAsync(() => textField.ValueChanged.InvokeAsync("NewSecret1!"));

        Assert.Equal("NewSecret1!", emitted);
    }

    [Fact]
    public void PasswordInput_CanHandle_MatchesByAttributeAndName()
    {
        var input = new UserPasswordInput();

        var byAttribute = new BlazorBase.CRUD.Components.CustomProperties.CustomPropertyContext(
            typeof(PasswordTestUser),
            typeof(UserModel).GetProperty(nameof(UserModel.Password))!,
            typeof(string),
            true);

        Assert.True(input.CanHandle(byAttribute));
    }

    private IRenderedComponent<UserPasswordInput> RenderDirect(
        PasswordTestUser model,
        Action<object?>? onValueChanged = null)
    {
        var property = typeof(UserModel).GetProperty(nameof(UserModel.Password))!;

        return Render<UserPasswordInput>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.Property, property)
            .Add(p => p.Value, property.GetValue(model))
            .Add(p => p.IsEditing, true)
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<object?>(this, value => onValueChanged?.Invoke(value))));
    }

    private IRenderedComponent<BasePropertyInput<PasswordTestUser>> RenderViaCard(
        PasswordTestUser model,
        PropertyFieldConfig<PasswordTestUser> field)
    {
        return Render<BasePropertyInput<PasswordTestUser>>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.FieldConfig, field)
            .Add(p => p.IsEditing, true));
    }

    private static PropertyFieldConfig<PasswordTestUser> Field(
        Expression<Func<PasswordTestUser, object?>> property)
    {
        return new BaseCardBuilder<PasswordTestUser>().Field(property).Build().Fields[0];
    }
}
