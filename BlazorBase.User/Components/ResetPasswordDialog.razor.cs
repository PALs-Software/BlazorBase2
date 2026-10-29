using BlazorBase.Components.Dialogs;
using Microsoft.Extensions.Localization;
using BlazorBase.User.Models;

namespace BlazorBase.User.Components;

/// <summary>
/// Dialog content that prompts for a new password and writes it to <see cref="ResetPasswordModel.NewPassword"/>.
/// Shows Fluent's own OK and Cancel buttons.
/// </summary>
public partial class ResetPasswordDialog(IStringLocalizer<ResetPasswordDialog> localizer) : BaseDialogInstance<ResetPasswordModel>
{
    #region Injects
    private readonly IStringLocalizer<ResetPasswordDialog> DialogLocalizer = localizer;
    #endregion

    protected override bool UseDefaultFooter => true;
}
