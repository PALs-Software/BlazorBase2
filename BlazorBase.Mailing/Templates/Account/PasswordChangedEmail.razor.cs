using System.Globalization;
using BlazorBase.Mailing.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace BlazorBase.Mailing.Templates.Account;

/// <summary>
/// The standard "your password was changed" notification: tells the account owner that the password
/// changed and to use "forgot password" or contact the administrator when it was not them. It carries
/// no link and no token. Rendered in the current UI culture, which <c>IAccountEmailBuilder</c> pins to
/// the recipient's language.
/// </summary>
public partial class PasswordChangedEmail(IStringLocalizer<PasswordChangedEmail> localizer)
{
    #region Injects
    private readonly IStringLocalizer<PasswordChangedEmail> Localizer = localizer;
    #endregion

    [Parameter, EditorRequired]
    public PasswordChangedEmailModel Model { get; set; } = new();

    [Parameter]
    public EmailBrandingOptions Branding { get; set; } = new();

    private string Subject => string.Format(Localizer["Subject"], Branding.BrandName);

    private static string LanguageCode => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    private string Greeting => string.IsNullOrWhiteSpace(Model.RecipientName)
        ? Localizer["GreetingNeutral"]
        : string.Format(Localizer["Greeting"], Model.RecipientName);
}
