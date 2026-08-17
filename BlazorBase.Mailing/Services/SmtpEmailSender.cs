using BlazorBase.Mailing.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BlazorBase.Mailing.Services;

/// <summary>
/// MailKit-based SMTP implementation of <see cref="IEmailSender"/>. Reads transport settings
/// from <see cref="SmtpSettings"/> via the options pattern.
/// </summary>
public class SmtpEmailSender(IOptions<SmtpSettings> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    #region Injects
    private readonly SmtpSettings Settings = options.Value;
    private readonly ILogger<SmtpEmailSender> Logger = logger;
    #endregion

    private static int InsecureConfigWarningLogged;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Settings.Host))
            throw new InvalidOperationException("SmtpSettings:Host is not configured.");

        if (string.IsNullOrWhiteSpace(Settings.FromEmail))
            throw new InvalidOperationException("SmtpSettings:FromEmail is not configured.");

        if (message.To.Count == 0)
            throw new ArgumentException("EmailMessage must have at least one recipient.", nameof(message));

        var mimeMessage = BuildMimeMessage(message);

        using var client = new SmtpClient();

        if (Settings.AllowInvalidServerCertificate)
            client.ServerCertificateValidationCallback = (_, _, _, _) => true;

        var resolvedTlsMode = ResolveTlsMode(Settings);
        var secureSocketOption = ToSecureSocketOptions(resolvedTlsMode);

        LogInsecureConfigurationWarningsOnce(resolvedTlsMode);

        await client.ConnectAsync(Settings.Host, Settings.Port, secureSocketOption, cancellationToken);

        if (!string.IsNullOrEmpty(Settings.Username))
            await client.AuthenticateAsync(Settings.Username, Settings.Password ?? string.Empty, cancellationToken);

        await client.SendAsync(mimeMessage, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        LogSendResult(message);
    }

    /// <summary>
    /// Guards <see cref="LogInsecureConfigurationWarnings"/> so an insecure configuration is reported
    /// at most once per process. <see cref="Settings"/> comes from <see cref="IOptions{TOptions}"/> and
    /// does not change per send, so repeating the warning on every send (the sender is scoped/transient)
    /// would just be noise.
    /// </summary>
    internal void LogInsecureConfigurationWarningsOnce(SmtpTlsMode resolvedTlsMode)
    {
        if (Interlocked.Exchange(ref InsecureConfigWarningLogged, 1) != 0)
            return;

        LogInsecureConfigurationWarnings(resolvedTlsMode);
    }

    /// <summary>
    /// Logs developer-diagnostic warnings for insecure SMTP configuration: disabled certificate
    /// validation (<see cref="SmtpSettings.AllowInvalidServerCertificate"/>) and TLS modes that can
    /// negotiate down to (or use) plaintext. Extracted so it can be exercised directly in tests
    /// without requiring a live SMTP connection.
    /// </summary>
    internal void LogInsecureConfigurationWarnings(SmtpTlsMode resolvedTlsMode)
    {
        if (Settings.AllowInvalidServerCertificate)
            Logger.LogWarning("TLS server-certificate validation is DISABLED for the SMTP connection to '{Host}'. This must not be used in production.", Settings.Host);

        if (resolvedTlsMode is SmtpTlsMode.Auto or SmtpTlsMode.None)
            Logger.LogWarning("SMTP TLS mode '{TlsMode}' for '{Host}' may negotiate down to (or use) plaintext. 'StartTls' or 'SslOnConnect' is recommended for production use.", resolvedTlsMode, Settings.Host);
    }

    /// <summary>
    /// Logs the outcome of a successful send. The default <see cref="LogLevel.Information"/> entry
    /// carries only the recipient count so no PII (addresses or subject) leaks into the default log
    /// level; the detailed form is only emitted at <see cref="LogLevel.Debug"/>.
    /// </summary>
    internal void LogSendResult(EmailMessage message)
    {
        Logger.LogInformation("Sent email to {RecipientCount} recipient(s).", message.To.Count);

        if (Logger.IsEnabled(LogLevel.Debug))
            Logger.LogDebug("Sent email '{Subject}' to {Recipients}", message.Subject, string.Join(", ", message.To));
    }

    /// <summary>
    /// Resolves the effective <see cref="SmtpTlsMode"/> for a connection: an explicit
    /// <see cref="SmtpSettings.TlsMode"/> always wins; otherwise the mode derives from the legacy
    /// <see cref="SmtpSettings.UseStartTls"/> flag for back-compat (default: <see cref="SmtpTlsMode.StartTls"/>).
    /// </summary>
    internal static SmtpTlsMode ResolveTlsMode(SmtpSettings settings)
        => settings.TlsMode ?? (settings.UseStartTls ? SmtpTlsMode.StartTls : SmtpTlsMode.Auto);

    /// <summary>
    /// Maps an <see cref="SmtpTlsMode"/> to the MailKit <see cref="SecureSocketOptions"/> it represents.
    /// </summary>
    internal static SecureSocketOptions ToSecureSocketOptions(SmtpTlsMode tlsMode) => tlsMode switch
    {
        SmtpTlsMode.StartTls => SecureSocketOptions.StartTls,
        SmtpTlsMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
        SmtpTlsMode.Auto => SecureSocketOptions.Auto,
        SmtpTlsMode.None => SecureSocketOptions.None,
        _ => throw new ArgumentOutOfRangeException(nameof(tlsMode), tlsMode, null),
    };

    internal MimeMessage BuildMimeMessage(EmailMessage message)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(Settings.FromName, Settings.FromEmail));

        foreach (var to in message.To)
            mimeMessage.To.Add(MailboxAddress.Parse(to));

        foreach (var cc in message.Cc)
            mimeMessage.Cc.Add(MailboxAddress.Parse(cc));

        foreach (var bcc in message.Bcc)
            mimeMessage.Bcc.Add(MailboxAddress.Parse(bcc));

        mimeMessage.Subject = message.Subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.PlainTextBody,
        };

        foreach (var attachment in message.Attachments)
            bodyBuilder.Attachments.Add(attachment.FileName, attachment.Content, MimeKit.ContentType.Parse(attachment.ContentType));

        mimeMessage.Body = bodyBuilder.ToMessageBody();
        return mimeMessage;
    }
}
