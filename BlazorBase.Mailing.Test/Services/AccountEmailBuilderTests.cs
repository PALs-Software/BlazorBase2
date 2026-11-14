using System.Globalization;
using System.Net;
using BlazorBase.Mailing.Models;
using BlazorBase.Mailing.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.Mailing.Test.Services;

/// <summary>
/// The standard account emails render through the real registration of <c>AddBlazorBaseMailing</c>:
/// subject and body follow the requested culture (English and German), the configured brand is used,
/// and links are HTML-encoded rather than concatenated into the markup.
/// </summary>
public sealed class AccountEmailBuilderTests
{
    private const string LinkWithSpecialCharacters = "https://app.example.com/invitation/accept?token=abc&next=\"<b>\"";
    private const string EncodedLink = "https://app.example.com/invitation/accept?token=abc&amp;next=&quot;&lt;b&gt;&quot;";

    private static async Task<EmailMessage> BuildAsync(Func<IAccountEmailBuilder, Task<EmailMessage>> build, string brandName = "ForkTest")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EmailBranding:BrandName"] = brandName,
                ["EmailBranding:HeaderColor"] = "#123456",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBlazorBaseMailing(configuration, dispatchMode: EmailDispatchMode.Inline);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        return await build(scope.ServiceProvider.GetRequiredService<IAccountEmailBuilder>());
    }

    private static InvitationEmailModel InvitationModel() => new()
    {
        RecipientName = "Alex",
        InvitedByName = "Sam",
        AcceptLink = LinkWithSpecialCharacters,
        ExpiresAt = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public async Task Invitation_renders_in_english_with_brand_and_encoded_link()
    {
        var message = await BuildAsync(builder => builder.BuildInvitationAsync("alex@example.com", InvitationModel(), CultureInfo.GetCultureInfo("en")));

        Assert.Equal(["alex@example.com"], message.To);
        Assert.Equal("You are invited to ForkTest", message.Subject);
        Assert.Contains("Sam has invited you to join ForkTest.", message.HtmlBody);
        Assert.Contains("Hello Alex,", message.HtmlBody);
        Assert.Contains("Accept invitation", message.HtmlBody);
        Assert.Contains("lang=\"en\"", message.HtmlBody);
        Assert.Contains("#123456", message.HtmlBody);
        Assert.Contains($"href=\"{EncodedLink}\"", message.HtmlBody);
        Assert.DoesNotContain("<b>", message.HtmlBody);
    }

    [Fact]
    public async Task Invitation_renders_in_german()
    {
        var message = await BuildAsync(builder => builder.BuildInvitationAsync("alex@example.com", InvitationModel(), CultureInfo.GetCultureInfo("de")));

        Assert.Equal("Einladung zu ForkTest", message.Subject);
        Assert.Contains("Sam hat Sie zu ForkTest eingeladen.", message.HtmlBody);
        Assert.Contains("Einladung annehmen", message.HtmlBody);
        Assert.Contains("20.11.2026", message.HtmlBody);
        Assert.Contains("lang=\"de\"", message.HtmlBody);
        Assert.Contains($"href=\"{EncodedLink}\"", message.HtmlBody);
    }

    [Fact]
    public async Task Invitation_without_names_uses_the_neutral_greeting()
    {
        var model = new InvitationEmailModel { AcceptLink = "https://app.example.com/a", ExpiresAt = DateTime.UtcNow };

        var message = await BuildAsync(builder => builder.BuildInvitationAsync("alex@example.com", model, CultureInfo.GetCultureInfo("en")));

        Assert.Contains("Hello,", message.HtmlBody);
        Assert.Contains("You have been invited to join ForkTest.", message.HtmlBody);
    }

    [Fact]
    public async Task Password_reset_renders_link_in_both_languages()
    {
        var model = new PasswordResetEmailModel { RecipientName = "Alex", ResetLink = LinkWithSpecialCharacters };

        var english = await BuildAsync(builder => builder.BuildPasswordResetAsync("alex@example.com", model, CultureInfo.GetCultureInfo("en")));
        var german = await BuildAsync(builder => builder.BuildPasswordResetAsync("alex@example.com", model, CultureInfo.GetCultureInfo("de")));

        Assert.Equal("Reset your ForkTest password", english.Subject);
        Assert.Contains("Choose a new password", english.HtmlBody);
        Assert.Contains($"href=\"{EncodedLink}\"", english.HtmlBody);
        Assert.Equal("Passwort für ForkTest zurücksetzen", german.Subject);
        Assert.Contains("Neues Passwort wählen", WebUtility.HtmlDecode(german.HtmlBody));
        Assert.Contains($"href=\"{EncodedLink}\"", german.HtmlBody);
    }

    [Fact]
    public async Task Password_reset_code_renders_the_encoded_code_instead_of_a_link()
    {
        var model = new PasswordResetEmailModel { ResetCode = "12<34" };

        var message = await BuildAsync(builder => builder.BuildPasswordResetAsync("alex@example.com", model, CultureInfo.GetCultureInfo("en")));

        Assert.Contains("12&lt;34", message.HtmlBody);
        Assert.DoesNotContain("class=\"button\"", message.HtmlBody);
    }

    [Fact]
    public async Task Email_confirmation_renders_link_in_both_languages()
    {
        var model = new EmailConfirmationEmailModel { RecipientName = "Alex", ConfirmationLink = LinkWithSpecialCharacters };

        var english = await BuildAsync(builder => builder.BuildEmailConfirmationAsync("alex@example.com", model, CultureInfo.GetCultureInfo("en")));
        var german = await BuildAsync(builder => builder.BuildEmailConfirmationAsync("alex@example.com", model, CultureInfo.GetCultureInfo("de")));

        Assert.Equal("Confirm your email address for ForkTest", english.Subject);
        Assert.Contains($"href=\"{EncodedLink}\"", english.HtmlBody);
        Assert.Equal("E-Mail-Adresse für ForkTest bestätigen", german.Subject);
        Assert.Contains("Bitte bestätigen Sie", WebUtility.HtmlDecode(german.HtmlBody));
    }

    [Fact]
    public async Task Brand_name_is_html_encoded()
    {
        var message = await BuildAsync(
            builder => builder.BuildInvitationAsync("alex@example.com", InvitationModel(), CultureInfo.GetCultureInfo("en")),
            brandName: "Fork & <Cast>");

        Assert.Contains("Fork &amp; &lt;Cast&gt;", message.HtmlBody);
        Assert.DoesNotContain("<Cast>", message.HtmlBody);
    }
}
