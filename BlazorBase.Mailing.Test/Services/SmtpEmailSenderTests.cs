using BlazorBase.Mailing.Models;
using BlazorBase.Mailing.Services;
using BlazorBase.Mailing.Test.Infrastructure;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using Xunit;

namespace BlazorBase.Mailing.Test.Services;

/// <summary>
/// Tests for <see cref="SmtpEmailSender"/>: the configuration/recipient guard clauses (which throw
/// before any network access) and the MimeKit message construction (<c>BuildMimeMessage</c>), without
/// requiring a live SMTP server.
/// </summary>
public sealed class SmtpEmailSenderTests
{
    private static SmtpEmailSender CreateSender(SmtpSettings settings)
        => new(Options.Create(settings), NullLogger<SmtpEmailSender>.Instance);

    private static (SmtpEmailSender Sender, CapturingLogger<SmtpEmailSender> Logger) CreateSenderWithCapturingLogger(SmtpSettings settings)
    {
        var logger = new CapturingLogger<SmtpEmailSender>();
        var sender = new SmtpEmailSender(Options.Create(settings), logger);
        return (sender, logger);
    }

    private static SmtpSettings ValidSettings() => new()
    {
        Host = "smtp.example.com",
        Port = 587,
        FromEmail = "noreply@example.com",
        FromName = "Example App",
    };

    [Fact]
    public async Task SendAsync_Throws_WhenHostMissing()
    {
        var sender = CreateSender(new SmtpSettings { FromEmail = "noreply@example.com" });
        var message = new EmailMessage { To = ["to@example.com"], Subject = "Hi" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(message));
    }

    [Fact]
    public async Task SendAsync_Throws_WhenFromEmailMissing()
    {
        var sender = CreateSender(new SmtpSettings { Host = "smtp.example.com" });
        var message = new EmailMessage { To = ["to@example.com"], Subject = "Hi" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(message));
    }

    [Fact]
    public async Task SendAsync_Throws_WhenNoRecipients()
    {
        var sender = CreateSender(ValidSettings());
        var message = new EmailMessage { Subject = "Hi" };

        await Assert.ThrowsAsync<ArgumentException>(() => sender.SendAsync(message));
    }

    [Fact]
    public void BuildMimeMessage_SetsFrom_FromSettings()
    {
        var sender = CreateSender(ValidSettings());
        var message = new EmailMessage { To = ["to@example.com"] };

        var mime = sender.BuildMimeMessage(message);

        var from = Assert.Single(mime.From.Mailboxes);
        Assert.Equal("Example App", from.Name);
        Assert.Equal("noreply@example.com", from.Address);
    }

    [Fact]
    public void BuildMimeMessage_AddsAllRecipients()
    {
        var sender = CreateSender(ValidSettings());
        var message = new EmailMessage
        {
            To = ["a@example.com", "b@example.com"],
            Cc = ["cc@example.com"],
            Bcc = ["bcc@example.com"],
        };

        var mime = sender.BuildMimeMessage(message);

        Assert.Equal(["a@example.com", "b@example.com"], mime.To.Mailboxes.Select(m => m.Address));
        Assert.Equal("cc@example.com", Assert.Single(mime.Cc.Mailboxes).Address);
        Assert.Equal("bcc@example.com", Assert.Single(mime.Bcc.Mailboxes).Address);
    }

    [Fact]
    public void BuildMimeMessage_SetsSubjectAndBodies()
    {
        var sender = CreateSender(ValidSettings());
        var message = new EmailMessage
        {
            To = ["to@example.com"],
            Subject = "Quarterly report",
            HtmlBody = "<p>Hello</p>",
            PlainTextBody = "Hello",
        };

        var mime = sender.BuildMimeMessage(message);

        Assert.Equal("Quarterly report", mime.Subject);
        Assert.Equal("<p>Hello</p>", mime.HtmlBody);
        Assert.Equal("Hello", mime.TextBody);
    }

    [Fact]
    public void BuildMimeMessage_AddsAttachments()
    {
        var sender = CreateSender(ValidSettings());
        var message = new EmailMessage
        {
            To = ["to@example.com"],
            HtmlBody = "<p>See attached</p>",
            Attachments = [new EmailAttachment("report.txt", "data"u8.ToArray(), "text/plain")],
        };

        var mime = sender.BuildMimeMessage(message);

        var part = Assert.Single(mime.Attachments.OfType<MimePart>());
        Assert.Equal("report.txt", part.FileName);
        Assert.Equal("text/plain", part.ContentType.MimeType);
    }

    [Theory]
    [InlineData(null, true, SmtpTlsMode.StartTls)]
    [InlineData(null, false, SmtpTlsMode.Auto)]
    [InlineData(SmtpTlsMode.SslOnConnect, false, SmtpTlsMode.SslOnConnect)]
    public void ResolveTlsMode_ResolvesExpectedMode(SmtpTlsMode? tlsMode, bool useStartTls, SmtpTlsMode expected)
    {
        var settings = new SmtpSettings { TlsMode = tlsMode, UseStartTls = useStartTls };

        var resolved = SmtpEmailSender.ResolveTlsMode(settings);

        Assert.Equal(expected, resolved);
    }

    [Theory]
    [InlineData(SmtpTlsMode.StartTls, SecureSocketOptions.StartTls)]
    [InlineData(SmtpTlsMode.SslOnConnect, SecureSocketOptions.SslOnConnect)]
    [InlineData(SmtpTlsMode.Auto, SecureSocketOptions.Auto)]
    [InlineData(SmtpTlsMode.None, SecureSocketOptions.None)]
    public void ToSecureSocketOptions_MapsEachMode(SmtpTlsMode tlsMode, SecureSocketOptions expected)
    {
        var resolved = SmtpEmailSender.ToSecureSocketOptions(tlsMode);

        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void LogInsecureConfigurationWarnings_WarnsWhenCertificateValidationDisabled()
    {
        var settings = ValidSettings();
        settings.AllowInvalidServerCertificate = true;
        var (sender, logger) = CreateSenderWithCapturingLogger(settings);

        sender.LogInsecureConfigurationWarnings(SmtpTlsMode.StartTls);

        var warning = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains("DISABLED", warning.Message);
        Assert.Contains(settings.Host, warning.Message);
    }

    [Fact]
    public void LogInsecureConfigurationWarnings_DoesNotWarn_WhenCertificateValidationEnabled_AndModeIsSecure()
    {
        var settings = ValidSettings();
        var (sender, logger) = CreateSenderWithCapturingLogger(settings);

        sender.LogInsecureConfigurationWarnings(SmtpTlsMode.StartTls);

        Assert.Empty(logger.Entries);
    }

    [Theory]
    [InlineData(SmtpTlsMode.Auto)]
    [InlineData(SmtpTlsMode.None)]
    public void LogInsecureConfigurationWarnings_WarnsOnDowngradeCapableModes(SmtpTlsMode tlsMode)
    {
        var settings = ValidSettings();
        var (sender, logger) = CreateSenderWithCapturingLogger(settings);

        sender.LogInsecureConfigurationWarnings(tlsMode);

        var warning = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains("plaintext", warning.Message);
    }

    [Theory]
    [InlineData(SmtpTlsMode.StartTls)]
    [InlineData(SmtpTlsMode.SslOnConnect)]
    public void LogInsecureConfigurationWarnings_DoesNotWarnOnSecureModes(SmtpTlsMode tlsMode)
    {
        var settings = ValidSettings();
        var (sender, logger) = CreateSenderWithCapturingLogger(settings);

        sender.LogInsecureConfigurationWarnings(tlsMode);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void LogInsecureConfigurationWarningsOnce_LogsOnlyOnce_AcrossMultipleInvocations()
    {
        var settings = ValidSettings();
        settings.AllowInvalidServerCertificate = true;
        var (sender, logger) = CreateSenderWithCapturingLogger(settings);

        sender.LogInsecureConfigurationWarningsOnce(SmtpTlsMode.StartTls);
        sender.LogInsecureConfigurationWarningsOnce(SmtpTlsMode.StartTls);
        sender.LogInsecureConfigurationWarningsOnce(SmtpTlsMode.StartTls);

        var warning = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains("DISABLED", warning.Message);
    }

    [Fact]
    public void LogSendResult_InformationLog_ContainsOnlyRecipientCount_NoAddressesOrSubject()
    {
        var settings = ValidSettings();
        var (sender, logger) = CreateSenderWithCapturingLogger(settings);
        var message = new EmailMessage
        {
            To = ["secret.recipient@example.com"],
            Subject = "Confidential subject",
        };

        sender.LogSendResult(message);

        var informationEntry = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Information);
        Assert.Equal("Sent email to 1 recipient(s).", informationEntry.Message);
        Assert.DoesNotContain("secret.recipient@example.com", informationEntry.Message);
        Assert.DoesNotContain("Confidential subject", informationEntry.Message);
    }

    [Fact]
    public void LogSendResult_DebugLog_ContainsSubjectAndRecipients()
    {
        var settings = ValidSettings();
        var (sender, logger) = CreateSenderWithCapturingLogger(settings);
        var message = new EmailMessage
        {
            To = ["secret.recipient@example.com"],
            Subject = "Confidential subject",
        };

        sender.LogSendResult(message);

        var debugEntry = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Debug);
        Assert.Contains("secret.recipient@example.com", debugEntry.Message);
        Assert.Contains("Confidential subject", debugEntry.Message);
    }
}
