using System.Reflection;
using BlazorBase.CRUD.Components.CustomProperties;
using BlazorBase.CRUD.Localization;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace BlazorBase.User.Components.UserManagementInputs;

/// <summary>
/// Role custom input for the generic CRUD card. Opts in for a string property marked with
/// <see cref="UserRoleInputAttribute"/> or named <c>Role</c>, rendering a select over the role names
/// supplied by the app-registered <see cref="IUserRoleProvider"/>. The bound value is a local string
/// so the value expression handed to the Fluent UI select is a clean member access.
/// </summary>
public partial class UserRoleInput : ComponentBase, IBaseCustomPropertyInput
{
    #region Injects
    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;
    #endregion

    private IUserRoleProvider? UserRoleProvider => ServiceProvider.GetService<IUserRoleProvider>();

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

    private IReadOnlyList<string> RoleNames =>
        UserRoleProvider?.GetRoleNames() ?? [];

    private string? RoleValue
    {
        get => Value as string;
        set => _ = ValueChanged.InvokeAsync(value);
    }

    /// <summary>
    /// Whether the bound value is one of the offered roles.
    /// </summary>
    /// <remarks>
    /// A new user arrives with <see cref="string.Empty"/>. The select then rendered its first option
    /// as though it had been chosen while the model stayed empty, so a required-role validation error
    /// appeared under a field that looked filled in. An empty option keeps the two in step, the same
    /// way <c>BasePropertyInput</c> does it for navigation properties. Defaulting to the first role
    /// instead would silently grant a role nobody picked — the wrong side to err on.
    /// </remarks>
    private bool HasKnownRole => Value is string role && RoleNames.Contains(role);

    /// <summary>
    /// Only when there is something to choose from. Without a registered role provider the select
    /// stays genuinely empty rather than inviting a choice that does not exist.
    /// </summary>
    private bool ShowPlaceholder => RoleNames.Count > 0 && !HasKnownRole;

    private string PlaceholderText =>
        LocalizerResolver.ResolveFramework(ServiceProvider)["SelectEmpty"].Value;

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

    public bool CanHandle(CustomPropertyContext context)
    {
        if (context.PropertyType != typeof(string))
            return false;

        return context.Property.GetCustomAttribute<UserRoleInputAttribute>() is not null
            || context.Property.Name == nameof(IBaseUser.Role);
    }
}
