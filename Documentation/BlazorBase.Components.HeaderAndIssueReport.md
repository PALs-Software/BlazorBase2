# BlazorBase.Components: header actions, issue report and client diagnostics

Package: `BlazorBase.Components` (browser-safe). Namespaces: `BlazorBase.Components.HeaderActions`, `BlazorBase.Components.IssueReport`, `BlazorBase.Components.Diagnostics`. Texts are localized in English (default) and German.

---

## Header actions

Plain icon buttons in the layout header, ordered, with access rules. They replace hand-written `HeaderContent` for buttons that open a dialog or panel.

### Item

`HeaderActionItem`:

| Property | Meaning |
|---|---|
| `Id` | Stable identifier. A later item with the same id replaces an earlier one. |
| `Icon` | FluentUI icon, normally `Size20.*`. |
| `Title` | Localized text; tooltip and `aria-label`. |
| `OnInvoke` | `Func<IServiceProvider, Task>` run on click or Enter/Space. |
| `Order` | Position; lower first, ties ordered by `Id`. |
| `RequiredRole` | Comma separated roles; the user needs at least one. Anonymous users never see the item. |
| `RequiredPolicy` | Authorization policy checked with `IAuthorizationService`; without that service the item is hidden. |
| `IsVisible` | Predicate evaluated before every render. |
| `IsActive` | Sets `aria-pressed` and a selected background, for buttons that toggle a panel. |
| `PopupKind` | `aria-haspopup` value, e.g. `dialog`. |

### Registration

```csharp
services.AddBlazorBaseComponents();   // registers IHeaderActionService

services.AddHeaderAction(new HeaderActionItem
{
    Id = "export",
    Icon = new Size20.Bug(),
    Title = "Export",
    Order = 50,
    RequiredRole = "Admin",
    OnInvoke = sp => sp.GetRequiredService<IExportService>().RunAsync(),
});

services.AddHeaderAction(sp =>                       // factory: build the title with a localizer
{
    var localizer = sp.GetRequiredService<IStringLocalizer<MyResources>>();
    return new HeaderActionItem { Id = "assistant", Icon = new Size20.Bug(), Title = localizer["Assistant"], OnInvoke = ... };
});
```

`IHeaderActionService` (scoped) lists the items and offers `Register`, `Unregister` and `Refresh` for run-time changes; `Refresh` re-evaluates visibility and pressed state, e.g. after a panel opened.

A layout can add items as well: `<BaseLayout HeaderActions="@items">`. Items from the parameter replace registered items with the same id.

### Layout behavior

- The row is rendered after `HeaderContent` and in front of the help, theme and settings buttons.
- Visibility is evaluated again when the service changes, the authentication state changes and the route changes.
- At 768 px and below only `MaxPhoneHeaderActions` (default 2) buttons stay in the header; the rest move into a "more" menu (`role="menu"`, closes on Escape, outside click or selection).
- Buttons are transparent icon `FluentButton`s with the class `base-header-control`: focusable, activated by Enter and Space, with an accessible name.
- A layout without registered services and without `HeaderActions` renders nothing.

### Layout overlays

`BaseLayout.LayoutOverlays` is a `RenderFragment` rendered inside `.main-content` after the main content and the help panel. It is a sibling in the page row, so a docked panel takes part in the flex layout and a fixed-position panel floats above it. The layout adds no wrapper.

---

## Issue report

A dialog that opens the user's mail program with a prepared mail to the support mailbox. Nothing is sent by the website.

### Registration

```csharp
services.AddBlazorBaseIssueReporting(options =>
{
    options.Recipient = "support@example.com";
    options.SiteName = "my.site";            // subject prefix [my.site]
    options.AppVersion = "1.14.0";           // default: entry assembly version
});
```

It registers `IIssueReportService`, `IIssueReportDiagnosticsCollector`, `IIssueReportBrowser`, the client diagnostics (see below) and a bug icon header action (`report-issue`, order 100). The button is hidden while `Recipient` is empty. `BaseLayout` renders the dialog provider the dialog needs (`FluentProviders`).

`IssueReportOptions`:

| Option | Default | Meaning |
|---|---|---|
| `Recipient` | empty | Mail address. Required. |
| `SiteName` | empty | Part of the subject prefix and of the `App` diagnostic line. |
| `SubjectPrefix` | `[SiteName]` | Replaces the prefix. |
| `AppVersion` | entry assembly | Shown in the diagnostics. |
| `Categories` | Bug, Question, Suggestion | Offered categories; an empty list removes the field. |
| `MaxUrlLength` | 1800 | Limit of the `mailto:` link. |
| `MaxErrorEntries` | 3 | Client errors listed. |
| `MaxSubjectSummaryLength` | 100 | Longer summaries are cut in the subject and repeated in full in the body. |
| `DescriptionHeading`, `DiagnosticsHeading` | Description, Diagnostics | Headings in the mail body (read by the support team, not localized). |
| `RegisterHeaderAction`, `HeaderActionOrder` | true, 100 | Header button. |

`IIssueReportService.OpenAsync(IssueReportDialogContent? content = null)` opens the dialog from code; `IssueReportDialogContent` pre-fills summary, description and category.

### Dialog

`BaseIssueReportDialog` has the fields summary (required), category (optional) and description. Below them the diagnostics preview shows exactly what is added, with a checkbox to leave it out.

- **Open in mail program** builds the link and opens it through the browser. When the window does not lose focus within 2.5 s the mail program is assumed not to have opened: the full text is copied to the clipboard and the dialog names the recipient. If the browser denies copying, the text is shown for manual copy.
- **Copy details** copies recipient, subject and the complete body, including diagnostics that did not fit into the link.
- The dialog stays open after opening the mail program and closes on **Close**, so the user always sees what happened. Screenshots are attached in the mail program; a hint says so.
- On phones the buttons are stacked, primary first.

### Mail format

`IssueReportMailtoBuilder.Build(options, input, diagnostics)` is a pure function and returns an `IssueReportMail` (`Subject`, `Body`, `MailtoUri`, `FullText`, `IsTruncated`, `ExceedsLimit`). Pass `null` diagnostics to leave them out.

```
mailto:<recipient>?subject=[site] Category: Summary&body=<encoded>

Description:
<text>

Diagnostics:
Page=…          Time=2026-10-28T13:05:11Z   App=site 1.14.0
Correlation=…   User=…                      Error1=…   (newest first)
Route=…         Culture=…                   Theme=…    Browser=…
```

Line breaks are encoded as `%0D%0A`. To stay within `MaxUrlLength` the builder tries, in order: all lines; error texts cut to 100 characters; then fewer lines, dropping from the end of the list above (browser first, page last); then no diagnostics. The user's description and summary are never cut. When the text alone exceeds the limit, `ExceedsLimit` is true and the dialog advises "Copy details". The 1,800 character default is a conservative value for mail programs that truncate long links; adjust it per site if your clients allow more.

---

## Client diagnostics

`IClientDiagnostics` keeps the last client errors (ring buffer, `ClientDiagnosticsOptions.Capacity`, default 10) and the last correlation id. Messages are single line, cut to `MaxMessageLength` (300) and never contain stack traces.

```csharp
services.AddBlazorBaseClientDiagnostics(options => options.Capacity = 20);
```

`AddBlazorBaseIssueReporting` calls it. The buffer is one instance per process in the browser and one per scope elsewhere, so a Blazor Server circuit never shows another user's errors. Pass `lifetime:` to override.

Recorders:

| Recorder | Records |
|---|---|
| `DiagnosticsErrorBoundary` | Use instead of `ErrorBoundary`; records each unhandled exception of its content. Works without registered diagnostics. |
| `ClientDiagnosticsLoggerProvider` | Registered automatically with the process-wide (browser) buffer; records logs at `MinimumLogLevel` (Error) and above. |
| `ClientDiagnosticsHandler` | `DelegatingHandler`. Attach with `.AddHttpMessageHandler<ClientDiagnosticsHandler>()`; stores the correlation id of every response (headers `X-Correlation-ID`, `X-Request-ID`, `Request-Id` in that order, `CorrelationHeaders`) and records HTTP 5xx and transport failures with the path only, without the query string. Needs the process-wide buffer, because a handler runs in its own scope (WebAssembly). |

Other code can call `RecordError` and `RecordCorrelationId` directly and subscribe to `Changed`.
