using System.Globalization;
using BlazorBase.Mailing.Globalization;
using BlazorBase.Mailing.Models;
using BlazorBase.Mailing.Templates.Account;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace BlazorBase.Mailing.Services;

/// <summary>
/// <see cref="IAccountEmailBuilder"/> over <see cref="IEmailTemplateRenderer"/>: renders the template
/// component in the requested culture and reads the matching subject from the template's own resources.
/// </summary>
public class AccountEmailBuilder(
    IEmailTemplateRenderer templateRenderer,
    IStringLocalizerFactory localizerFactory,
    IOptions<EmailBrandingOptions> brandingOptions) : IAccountEmailBuilder
{
    #region Injects
    private readonly IEmailTemplateRenderer TemplateRenderer = templateRenderer;
    private readonly IStringLocalizerFactory LocalizerFactory = localizerFactory;
    private readonly IOptions<EmailBrandingOptions> BrandingOptions = brandingOptions;
    #endregion

    private const string SubjectResourceKey = "Subject";

    public Task<EmailMessage> BuildInvitationAsync(string recipientEmail, InvitationEmailModel model, CultureInfo? culture = null) =>
        BuildAsync<InvitationEmail>(recipientEmail, model, culture);

    public Task<EmailMessage> BuildPasswordResetAsync(string recipientEmail, PasswordResetEmailModel model, CultureInfo? culture = null) =>
        BuildAsync<PasswordResetEmail>(recipientEmail, model, culture);

    public Task<EmailMessage> BuildEmailConfirmationAsync(string recipientEmail, EmailConfirmationEmailModel model, CultureInfo? culture = null) =>
        BuildAsync<EmailConfirmationEmail>(recipientEmail, model, culture);

    public Task<EmailMessage> BuildPasswordChangedAsync(string recipientEmail, PasswordChangedEmailModel model, CultureInfo? culture = null) =>
        BuildAsync<PasswordChangedEmail>(recipientEmail, model, culture);

    private async Task<EmailMessage> BuildAsync<TTemplate>(string recipientEmail, object model, CultureInfo? culture)
        where TTemplate : IComponent
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmail);
        ArgumentNullException.ThrowIfNull(model);

        var branding = BrandingOptions.Value;
        var parameters = new Dictionary<string, object?>
        {
            ["Model"] = model,
            ["Branding"] = branding,
        };

        var htmlBody = await TemplateRenderer.RenderAsync<TTemplate>(parameters, culture);

        return new EmailMessage
        {
            To = [recipientEmail],
            Subject = ResolveSubject<TTemplate>(branding.BrandName, culture),
            HtmlBody = htmlBody,
        };
    }

    private string ResolveSubject<TTemplate>(string brandName, CultureInfo? culture)
    {
        using var cultureScope = new CultureScope(culture);
        var localizer = LocalizerFactory.Create(typeof(TTemplate));
        return string.Format(localizer[SubjectResourceKey], brandName);
    }
}
