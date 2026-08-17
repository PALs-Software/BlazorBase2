using BlazorBase.CRUD.Extensions;
using BlazorBase.User.Components.UserManagementInputs;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.Services;

public static class BlazorBaseUserManagementServiceCollectionExtensions
{
    /// <summary>
    /// Registers the user-management custom property inputs (password and role) with the generic
    /// CRUD card via <c>AddBlazorBaseCustomInput</c>. After this call, a <c>BaseCard</c>/<c>BaseList</c>
    /// rendering a user model picks up the write-only password box and the role select automatically.
    /// </summary>
    /// <remarks>
    /// The role input needs the role names; register an <see cref="IUserRoleProvider"/> separately via
    /// <see cref="AddBlazorBaseUserRoleProvider{TProvider}"/> (or your own registration) so the app
    /// supplies its own roles. Without a provider the role select simply renders no options.
    /// </remarks>
    public static IServiceCollection AddBlazorBaseUserManagementInputs(this IServiceCollection services)
    {
        services.AddBlazorBaseCustomInput<UserPasswordInput>();
        services.AddBlazorBaseCustomInput<UserRoleInput>();
        return services;
    }

    /// <summary>
    /// Registers the app's <see cref="IUserRoleProvider"/> that supplies the role names shown by the
    /// role input. Kept separate from <see cref="AddBlazorBaseUserManagementInputs"/> so the role set
    /// stays an explicit app concern.
    /// </summary>
    public static IServiceCollection AddBlazorBaseUserRoleProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IUserRoleProvider
    {
        services.AddScoped<IUserRoleProvider, TProvider>();
        return services;
    }
}
