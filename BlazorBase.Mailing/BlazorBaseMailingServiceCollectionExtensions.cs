using BlazorBase.Mailing.Models;
using BlazorBase.Mailing.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorBase.Mailing;

public static class BlazorBaseMailingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SmtpSettings"/> via the options pattern, the built-in
    /// <see cref="HtmlRenderer"/>, a <see cref="IEmailTemplateRenderer"/> implementation that uses it,
    /// a MailKit-based <see cref="IEmailSender"/>, and an <see cref="IEmailDispatcher"/> selected by
    /// <paramref name="dispatchMode"/>.
    /// </summary>
    /// <param name="sectionName">Configuration section name for <see cref="SmtpSettings"/>. Defaults to "Smtp".</param>
    /// <param name="dispatchMode">
    /// Defaults to <see cref="EmailDispatchMode.Background"/> so a send never adds its latency, or a
    /// transport failure, to the caller's response unless the host explicitly opts back into
    /// <see cref="EmailDispatchMode.Inline"/>.
    /// </param>
    /// <param name="dispatchSectionName">
    /// Configuration section name for <see cref="BackgroundEmailDispatchOptions"/>, bound the same
    /// way <paramref name="sectionName"/> binds <see cref="SmtpSettings"/>. Defaults to "EmailDispatch".
    /// Unused when <paramref name="dispatchMode"/> is <see cref="EmailDispatchMode.Inline"/>.
    /// </param>
    public static IServiceCollection AddBlazorBaseMailing(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "Smtp",
        EmailDispatchMode dispatchMode = EmailDispatchMode.Background,
        string dispatchSectionName = "EmailDispatch")
    {
        services.Configure<SmtpSettings>(configuration.GetSection(sectionName));

        services.AddScoped<HtmlRenderer>(provider =>
            new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>()));

        services.AddScoped<IEmailTemplateRenderer, RazorEmailTemplateRenderer>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        if (dispatchMode == EmailDispatchMode.Background)
        {
            services.Configure<BackgroundEmailDispatchOptions>(configuration.GetSection(dispatchSectionName));
            services.AddSingleton<IBackgroundEmailQueue, ChannelBackgroundEmailQueue>();
            services.AddScoped<IEmailDispatcher, BackgroundEmailDispatcher>();
            services.AddHostedService<BackgroundEmailSenderService>();
        }
        else
        {
            services.AddScoped<IEmailDispatcher, InlineEmailDispatcher>();
        }

        return services;
    }
}
