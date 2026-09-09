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
`SmtpEmailSender` and `IEmailTemplateRenderer`, and registers an `IEmailDispatcher` selected by the
`dispatchMode` parameter — see [Dispatch: keeping the send off the request path](#dispatch-keeping-the-send-off-the-request-path).

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

---

## Dispatch: keeping the send off the request path

Awaiting `IEmailSender.SendAsync` inside a request handler ties the response to the transport twice
over: the response time follows the send, and a transport failure becomes the response. In an
anti-enumeration endpoint such as `forgot-password` both are oracles — the branch that sends mail is
the branch that found an account, so either signal tells a caller which addresses are registered.

`IEmailDispatcher` is the seam that breaks that coupling. Its contract is *hand-off, not delivery*: a
completed `DispatchAsync` means the message was accepted for sending, never that it arrived. Callers
that need delivery confirmation must not use it.

```csharp
public class WelcomeService(IEmailDispatcher emailDispatcher)
{
    private readonly IEmailDispatcher EmailDispatcher = emailDispatcher;

    public ValueTask SendWelcomeAsync(string toAddress, string html, CancellationToken cancellationToken) =>
        EmailDispatcher.DispatchAsync(new EmailMessage
        {
            To = { toAddress },
            Subject = "Welcome",
            HtmlBody = html,
        }, cancellationToken);
}
```

| `EmailDispatchMode` | Behaviour |
|---|---|
| `Background` (default) | `BackgroundEmailDispatcher` writes the message to a bounded channel. `BackgroundEmailSenderService`, a hosted service, drains it and performs the send from a fresh DI scope, logging failures at `Error` and continuing with the next message. |
| `Inline` | `InlineEmailDispatcher` awaits `IEmailSender.SendAsync` directly and propagates its exception — the pre-dispatcher behaviour, for a host that needs a send failure to surface in the response. |

```csharp
builder.Services.AddBlazorBaseMailing(builder.Configuration, dispatchMode: EmailDispatchMode.Inline);
```

Background is the default because the safe behaviour should not require the host to know it has to ask
for it.

The queue is bounded (`BackgroundEmailDispatchOptions.QueueCapacity`, default `1000`, bound from the
`"EmailDispatch"` configuration section) so a stuck transport cannot exhaust memory, and it uses
`BoundedChannelFullMode.Wait` rather than a drop mode: silently discarding a password-reset mail is
worse than back-pressure, and a full queue is a saturation condition that affects every caller
equally rather than a signal about any one address.

Two consequences a host has to accept in exchange:

- **A send failure never reaches the caller.** It is logged; nothing else surfaces it. Monitor the
  `BackgroundEmailSenderService` log category.
- **Messages still queued at shutdown are lost.** The queue is in-process and not persisted. A host
  that cannot lose a message needs a durable outbox, not this queue.

