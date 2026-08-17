namespace BlazorBase.Mailing.Models;

/// <summary>
/// SMTP transport configuration bound from the "Smtp" section of the host's configuration.
/// </summary>
public class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public SmtpTlsMode? TlsMode { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public bool AllowInvalidServerCertificate { get; set; }
}
