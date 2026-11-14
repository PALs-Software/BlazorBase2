using System.Globalization;
using BlazorBase.Mailing.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace BlazorBase.Mailing.Templates.Account;

/// <summary>
/// The standard password reset email: a button and copyable link to the reset page, or the reset code
/// when <see cref="PasswordResetEmailModel.ResetLink"/> is empty. Rendered in the current UI culture.
/// </summary>
public partial class PasswordResetEmail(IStringLocalizer<PasswordResetEmail> localizer)
{
    #region Injects
    private readonly IStringLocalizer<PasswordResetEmail> Localizer = localizer;
    #endregion

    [Parameter, EditorRequired]
    public PasswordResetEmailModel Model { get; set; } = new();

    [Parameter]
    public EmailBrandingOptions Branding { get; set; } = new();

    private string Subject => string.Format(Localizer["Subject"], Branding.BrandName);

    private static string LanguageCode => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    private string Greeting => string.IsNullOrWhiteSpace(Model.RecipientName)
        ? Localizer["GreetingNeutral"]
        : string.Format(Localizer["Greeting"], Model.RecipientName);
}
