using AppTemplate.Shared.Modules.Authentication;
using BlazorBase.User.Services;

namespace AppTemplate.Client.Modules.Authentication.Services;

/// <summary>
/// Supplies the role names the framework's user-management card offers in its role select.
/// Roles are an application concern, so the framework reads them through this seam.
/// </summary>
public class AppUserRoleProvider : IUserRoleProvider
{
    public IReadOnlyList<string> GetRoleNames() => RoleConstants.All;
}
