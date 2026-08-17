using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using BlazorBase.User.Models;

namespace BlazorBase.User.Components;

public partial class ResetPasswordDialog(IStringLocalizer<ResetPasswordDialog> localizer)
{
    #region Injects
    private readonly IStringLocalizer<ResetPasswordDialog> Localizer = localizer;
    #endregion

    [Parameter]
    public ResetPasswordModel Content { get; set; } = new();
}
