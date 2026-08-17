using BlazorBase.Mailing;
using BlazorBase.Mailing.Models;
using BlazorBase.Mailing.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.Mailing.Test;

/// <summary>
/// Verifies that <see cref="BlazorBaseMailingServiceCollectionExtensions.AddBlazorBaseMailing"/>
/// registers the renderer/sender services with the expected lifetimes and binds
/// <see cref="SmtpSettings"/> from the configured section.
/// </summary>
public sealed class BlazorBaseMailingServiceCollectionExtensionsTests
{
    private static IConfiguration BuildConfiguration(string section = "Smtp") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{section}:Host"] = "smtp.example.com",
                [$"{section}:Port"] = "25",
                [$"{section}:UseStartTls"] = "false",
                [$"{section}:FromEmail"] = "noreply@example.com",
            })
            .Build();

    [Fact]
    public void RegistersServicesAsScoped_WithExpectedImplementations()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddBlazorBaseMailing(BuildConfiguration());

        var renderer = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IEmailTemplateRenderer));
        Assert.Equal(ServiceLifetime.Scoped, renderer.Lifetime);
        Assert.Equal(typeof(RazorEmailTemplateRenderer), renderer.ImplementationType);

        var sender = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IEmailSender));
        Assert.Equal(ServiceLifetime.Scoped, sender.Lifetime);
        Assert.Equal(typeof(SmtpEmailSender), sender.ImplementationType);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(HtmlRenderer));
    }

    [Fact]
    public async Task ResolvesRendererAndSender()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBlazorBaseMailing(BuildConfiguration());

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        Assert.IsType<RazorEmailTemplateRenderer>(scope.ServiceProvider.GetRequiredService<IEmailTemplateRenderer>());
        Assert.IsType<SmtpEmailSender>(scope.ServiceProvider.GetRequiredService<IEmailSender>());
    }

    [Fact]
    public void BindsSmtpSettings_FromDefaultSection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBlazorBaseMailing(BuildConfiguration());

        using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IOptions<SmtpSettings>>().Value;

        Assert.Equal("smtp.example.com", settings.Host);
        Assert.Equal(25, settings.Port);
        Assert.False(settings.UseStartTls);
        Assert.Equal("noreply@example.com", settings.FromEmail);
    }

    [Fact]
    public void BindsSmtpSettings_FromCustomSection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBlazorBaseMailing(BuildConfiguration("Mail"), "Mail");

        using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IOptions<SmtpSettings>>().Value;

        Assert.Equal("smtp.example.com", settings.Host);
        Assert.Equal(25, settings.Port);
    }
}
