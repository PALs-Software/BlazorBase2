namespace BlazorBase.Mailing.Models;

/// <summary>
/// Explicit SMTP transport-security negotiation mode used when connecting to the SMTP server.
/// </summary>
public enum SmtpTlsMode
{
    /// <summary>
    /// Connect in plaintext and explicitly upgrade to TLS via the STARTTLS command. Recommended for
    /// servers listening on the standard submission port (587).
    /// </summary>
    StartTls,

    /// <summary>
    /// Establish TLS immediately on connect (implicit TLS), typically used on port 465.
    /// </summary>
    SslOnConnect,

    /// <summary>
    /// Let MailKit infer the appropriate option from the port number. May silently negotiate down to
    /// plaintext for unrecognized ports.
    /// </summary>
    Auto,

    /// <summary>
    /// Never use TLS; the connection stays in plaintext for its entire lifetime. Not recommended for
    /// production use.
    /// </summary>
    None,
}
