using System.Reflection;
using BlazorBase.CRUD.Components.CustomProperties;
using BlazorBase.User.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace BlazorBase.User.Components.UserManagementInputs;

/// <summary>
/// Write-only password custom input for the generic CRUD card. Opts in for a string property marked
/// with <see cref="UserPasswordInputAttribute"/> or named <c>Password</c>. The bound value is a
/// local string so the value expression handed to the Fluent UI input is a clean member access,
/// which keeps Fluent UI's <c>FieldIdentifier</c> derivation from throwing on the user model. The
/// field starts blank; only a non-blank entry is pushed back to the model, so leaving it empty keeps
/// the current password.
/// </summary>
public partial class UserPasswordInput : ComponentBase, IBaseCustomPropertyInput
{
    [Parameter]
    public object Model { get; set; } = default!;

    [Parameter]
    public PropertyInfo Property { get; set; } = default!;

    [Parameter]
    public object? Value { get; set; }

    [Parameter]
    public EventCallback<object?> ValueChanged { get; set; }

    [Parameter]
    public bool IsEditing { get; set; }

    [Parameter]
    public bool ReadOnly { get; set; }

    [Parameter]
    public IStringLocalizer? Localizer { get; set; }

    private string? localPassword;

    private string? PasswordValue
    {
        get => localPassword;
        set
        {
            localPassword = value;
            _ = PushValueAsync(value);
        }
    }

    private string FieldLabel
    {
        get
        {
            if (Localizer is null)
                return Property.Name;

            var localized = Localizer[Property.Name];
            return localized.ResourceNotFound ? Property.Name : localized.Value;
        }
    }

    private string? PlaceholderText
    {
        get
        {
            if (Localizer is null)
                return null;

            var localized = Localizer["UserPasswordPlaceholder"];
            return localized.ResourceNotFound ? null : localized.Value;
        }
    }

    public bool CanHandle(CustomPropertyContext context)
    {
        if (context.PropertyType != typeof(string))
            return false;

        return context.Property.GetCustomAttribute<UserPasswordInputAttribute>() is not null
            || context.Property.Name == nameof(IBaseUser.Password);
    }

    private async Task PushValueAsync(string? value)
    {
        var normalized = string.IsNullOrEmpty(value) ? null : value;
        await ValueChanged.InvokeAsync(normalized);
    }
}
