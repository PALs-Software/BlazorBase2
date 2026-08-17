using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using BlazorBase.User.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace BlazorBase.User.Components;

/// <summary>
/// Declarative detail card for a <see cref="UserModel"/>, linked to <c>UserManagementView</c> via the
/// BaseList <c>CardType</c> seam. The role and password fields render through the registered custom
/// property inputs (see <c>AddBlazorBaseUserManagementInputs</c>); the host supplies the field labels
/// through the localizer passed down from the list.
/// </summary>
public partial class UserCard : ComponentBase
{
    [Parameter]
    public UserModel Model { get; set; } = new();

    [Parameter]
    public IBaseDataProvider<UserModel>? DataProvider { get; set; }

    [Parameter]
    public IStringLocalizer? Localizer { get; set; }

    [Parameter]
    public BaseCardConfiguration<UserModel>? Configuration { get; set; }

    [Parameter]
    public EventCallback<UserModel> OnAfterSave { get; set; }

    [Parameter]
    public EventCallback OnCancelled { get; set; }

    private string Localize(string key) => Localizer is null ? key : Localizer[key].Value;
}
