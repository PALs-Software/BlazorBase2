using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace BlazorBase.User.Pages;

/// <summary>
/// Generic, reusable user-management UI. Renders the full user CRUD experience over the framework
/// <c>BaseList&lt;UserModel&gt;</c>: a list with the core user columns plus create/edit/delete through
/// the separate <see cref="BlazorBase.User.Components.UserCard"/> (linked via the BaseList
/// <c>CardType</c> seam). The card relies on the registered password and role custom inputs (see
/// <c>AddBlazorBaseUserManagementInputs</c>). The component carries no route, layout or authorization
/// of its own; the host app supplies those by hosting it on a page.
/// </summary>
public partial class UserManagementView(IStringLocalizer<UserManagementView> localizer) : ComponentBase
{
    #region Injects

    private readonly IStringLocalizer<UserManagementView> Localizer = localizer;

    #endregion
}
