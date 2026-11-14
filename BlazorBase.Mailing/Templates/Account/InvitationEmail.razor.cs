using System.Globalization;
using BlazorBase.Mailing.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace BlazorBase.Mailing.Templates.Account;

/// <summary>
/// The standard invitation email: greets the invited person, names who invited them, and carries the
/// single-use accept link as a button and as copyable text, plus its expiry date. Rendered in the
/// current UI culture, which <c>IAccountEmailBuilder</c> pins to the recipient's language.
/// </summary>
public partial class InvitationEmail(IStringLocalizer<InvitationEmail> localizer)
{
    #region Injects
    private readonly IStringLocalizer<InvitationEmail> Localizer = localizer;
    #endregion

    [Parameter, EditorRequired]
    public InvitationEmailModel Model { get; set; } = new();

    [Parameter]
    public EmailBrandingOptions Branding { get; set; } = new();

    private string Subject => string.Format(Localizer["Subject"], Branding.BrandName);

    private string Heading => string.Format(Localizer["Heading"], Branding.BrandName);

    private static string LanguageCode => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    private string Greeting => string.IsNullOrWhiteSpace(Model.RecipientName)
        ? Localizer["GreetingNeutral"]
        : string.Format(Localizer["Greeting"], Model.RecipientName);

    private string Introduction => string.IsNullOrWhiteSpace(Model.InvitedByName)
        ? string.Format(Localizer["Introduction"], Branding.BrandName)
        : string.Format(Localizer["IntroductionWithInviter"], Model.InvitedByName, Branding.BrandName);

    private string ExpiryText => string.Format(Localizer["Expiry"], Model.ExpiresAt.ToString("d", CultureInfo.CurrentCulture));
}
