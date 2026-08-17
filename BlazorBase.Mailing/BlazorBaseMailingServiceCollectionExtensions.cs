using BlazorBase.Mailing.Models;
using BlazorBase.Mailing.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorBase.Mailing;

public static class BlazorBaseMailingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SmtpSettings"/> via the options pattern, the built-in
    /// <see cref="HtmlRenderer"/>, a <see cref="IEmailTemplateRenderer"/> implementation that uses it,
    /// and a MailKit-based <see cref="IEmailSender"/>.
    /// </summary>
    /// <param name="sectionName">Configuration section name. Defaults to "Smtp".</param>
    public static IServiceCollection AddBlazorBaseMailing(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "Smtp")
    {
        services.Configure<SmtpSettings>(configuration.GetSection(sectionName));

        services.AddScoped<HtmlRenderer>(provider =>
            new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>()));

        services.AddScoped<IEmailTemplateRenderer, RazorEmailTemplateRenderer>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        return services;
    }
}
