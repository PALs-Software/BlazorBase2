using System.Globalization;
using BlazorBase.Mailing.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace BlazorBase.Mailing.Templates.Account;

/// <summary>
/// The standard email confirmation email: a button and copyable link to the client's confirm-email
/// page. Rendered in the current UI culture.
/// </summary>
public partial class EmailConfirmationEmail(IStringLocalizer<EmailConfirmationEmail> localizer)
{
    #region Injects
    private readonly IStringLocalizer<EmailConfirmationEmail> Localizer = localizer;
    #endregion

    [Parameter, EditorRequired]
    public EmailConfirmationEmailModel Model { get; set; } = new();

    [Parameter]
    public EmailBrandingOptions Branding { get; set; } = new();

    private string Subject => string.Format(Localizer["Subject"], Branding.BrandName);

    private static string LanguageCode => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    private string Greeting => string.IsNullOrWhiteSpace(Model.RecipientName)
        ? Localizer["GreetingNeutral"]
        : string.Format(Localizer["Greeting"], Model.RecipientName);
}
