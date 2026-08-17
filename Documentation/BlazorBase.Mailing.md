# BlazorBase.Mailing

A small, standalone email module: a MailKit-based SMTP `IEmailSender` wired from the options pattern,
plus an `IEmailTemplateRenderer` that renders Razor components to HTML. It has no dependency on the
other BlazorBase libraries.

> Target framework: `net10.0`.

---

## Registration

```csharp
// Program.cs — binds SmtpSettings from the "Smtp" configuration section by default
builder.Services.AddBlazorBaseMailing(builder.Configuration);
// or a custom section name:
builder.Services.AddBlazorBaseMailing(builder.Configuration, sectionName: "Email");
```

`AddBlazorBaseMailing` binds `SmtpSettings` (options pattern), registers `IEmailSender` →
`SmtpEmailSender` and `IEmailTemplateRenderer`.

```json
// appsettings.json
{
  "Smtp": {
    "Host": "smtp.example.com",
    "Port": 587,
    "TlsMode": "StartTls",
    "UseStartTls": true,
    "Username": "apikey",
    "Password": "<from-secrets>",
    "FromEmail": "noreply@example.com",
    "FromName": "Example",
    "AllowInvalidServerCertificate": false
  }
}
```

---

## Configuration: `SmtpSettings`

| Key | Type | Default | Description |
|---|---|---|---|
| `Host` | string | `""` (required) | SMTP server host. `SendAsync` throws if empty. |
| `Port` | int | `587` | SMTP port. |
| `TlsMode` | `SmtpTlsMode?` | `null` | Explicit transport security (see below). When `null`, the mode is derived from `UseStartTls` for back-compat. |
| `UseStartTls` | bool | `true` | Legacy toggle. `true` → `StartTls`; `false` → `Auto`. Superseded by `TlsMode` when that is set. |
| `Username` | string? | `null` | SMTP auth user; when empty, no `AUTHENTICATE` is sent. |
| `Password` | string? | `null` | SMTP auth password (store in a secret manager, never in source). |
| `FromEmail` | string | `""` (required) | Envelope/`From` address. `SendAsync` throws if empty. |
| `FromName` | string | `""` | Display name for the `From` address. |
| `AllowInvalidServerCertificate` | bool | `false` | When `true`, TLS server-certificate validation is **disabled** — see the security note below. |

### Transport security — `SmtpTlsMode` (MAIL-02)

`SmtpTlsMode` maps to MailKit's `SecureSocketOptions`:

| `TlsMode` | Behavior |
|---|---|
| `StartTls` | Require STARTTLS (fails if the server does not offer it). **Recommended / default.** |
| `SslOnConnect` | Implicit TLS on connect (typically port 465). |
| `Auto` | Negotiate; **may fall back to plaintext** if the server does not advertise TLS. |
| `None` | No transport encryption (plaintext). |

The default (`UseStartTls = true`, no `TlsMode`) resolves to `StartTls` and is unchanged from before. If
the resolved mode is `Auto` or `None`, `SmtpEmailSender` logs a one-time **Warning** at send time that the
connection may use/negotiate down to plaintext — the previous silent `UseStartTls=false → Auto` downgrade
is no longer silent.

### Security notes

- **`AllowInvalidServerCertificate` (MAIL-01):** enabling this disables TLS certificate validation
  entirely and exposes the connection to man-in-the-middle attacks. It is opt-in and off by default; when
  enabled, `SmtpEmailSender` logs a one-time **Warning** naming the host. Use it only against a local/dev
  relay, never in production.
- **Logging / PII (MAIL-03):** a successful send logs only the recipient **count** at `Information` level.
  The subject line and the full recipient addresses are logged only at `Debug` level (behind an
  `IsEnabled(LogLevel.Debug)` guard) — **enabling `Debug` logging in production will surface recipient PII
  and message subjects in your logs.** Keep the mailing category at `Information` or higher in production.

---

## Sending mail

```csharp
public class WelcomeService(IEmailSender emailSender)
{
    private readonly IEmailSender EmailSender = emailSender;

    public Task SendWelcomeAsync(string toAddress, string html, CancellationToken cancellationToken) =>
        EmailSender.SendAsync(new EmailMessage
        {
            To = { toAddress },
            Subject = "Welcome",
            HtmlBody = html,
        }, cancellationToken);
}
```

`EmailMessage` carries `To`/`Cc`/`Bcc` lists, `Subject`, `HtmlBody`, `PlainTextBody`, and `Attachments`.
`SendAsync` throws `ArgumentException` when there is no recipient, and `InvalidOperationException` when
`Host`/`FromEmail` are unconfigured.

The `SmtpClient` connect/authenticate/send path is created inline per send; there is no injectable client
seam, so those network calls are not unit-testable — the TLS-mode resolution, MIME building, and log
content are covered by unit tests instead.
