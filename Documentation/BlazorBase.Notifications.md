# BlazorBase.Notifications

In-app notifications and native Web Push delivery, split across two libraries the same way
**BlazorBase.Files** and **BlazorBase.Speech** are: a browser-safe Razor Class Library plus an
ASP.NET Core server companion.

| Library | Type | Role |
|---|---|---|
| **BlazorBase.Notifications** | Razor Class Library (net10.0, browser-safe) | DTOs, HTTP clients, Web Push JS interop, `NotificationBell` / `PushSettingsToggle` components, the push service-worker handler |
| **BlazorBase.Notifications.Server** | ASP.NET Core library (net10.0) | `PushSubscription` entity, VAPID-based Web Push sending, the REST surface both libraries share |

---

## Architecture: polling + Web Push, not SignalR

Delivery is **polling (default 60 s) plus Web Push**, deliberately not SignalR, SSE, or long
polling:

- The in-app inbox (`NotificationBell`) is a plain `GET /api/notifications/unread` polled on an
  interval, refreshed immediately on tab focus and on a message from the push service worker. It
  needs no open connection and survives a server restart or a load-balancer switch without any
  reconnect logic.
- Native OS notifications — the ones that appear even with every browser tab closed — are only
  reachable through the browser's Push API, which requires VAPID and a service worker regardless of
  whether the in-app channel is polling or a persistent connection. Once that channel exists, running
  a second, persistent channel (SignalR) only for the in-app list would duplicate it for no benefit: a
  60-second staleness window on an inbox list is not worth the server-side connection/scale-out cost
  SignalR adds.
- Notifications remain visible in the in-app list after a push fires, so a user who missed or
  dismissed the native toast can still find it.

Push payloads deliberately carry only `title`, a short `body`, a `url` and a `tag` — never the full
notification detail — so a compromised push service or endpoint never exposes sensitive content; the
app loads the detail itself after the user clicks through.

---

## Overview

### Client (`BlazorBase.Notifications`)

| Concern | Type |
|---|---|
| Inbox DTO | `BlazorBase.Notifications.Models.InboxNotificationDto` |
| Push subscription DTO | `BlazorBase.Notifications.Models.PushSubscriptionDto` |
| VAPID public key response | `BlazorBase.Notifications.Models.PushPublicKeyResponse` |
| Enable/disable request | `BlazorBase.Notifications.Models.SetPushSubscriptionEnabledRequest` |
| Client options | `BlazorBase.Notifications.Models.NotificationClientOptions` |
| Inbox HTTP client | `BlazorBase.Notifications.Services.INotificationInboxClient` / `HttpNotificationInboxClient` |
| Push subscription HTTP client | `BlazorBase.Notifications.Services.IPushSubscriptionClient` / `HttpPushSubscriptionClient` |
| Web Push JS interop | `BlazorBase.Notifications.Interop.IWebPushClient` / `WebPushClient` |
| Tab-focus / push-message refresh interop | `BlazorBase.Notifications.Interop.INotificationRefreshInterop` / `NotificationRefreshInterop` |
| Inbox bell + popover | `BlazorBase.Notifications.Components.NotificationBell.NotificationBell` |
| Native-notifications settings switch | `BlazorBase.Notifications.Components.PushSettingsToggle.PushSettingsToggle` |
| DI registration | `AddBlazorBaseNotifications(Action<NotificationClientOptions>?)` |
| JS interop module | `wwwroot/blazorbase-notifications.js` |
| Push service-worker handler | `wwwroot/blazorbase-push-sw.js` |

### Server (`BlazorBase.Notifications.Server`)

| Concern | Type |
|---|---|
| Subscription entity | `BlazorBase.Notifications.Server.Entities.PushSubscription` |
| EF Core model extension | `BlazorBase.Notifications.Server.Data.PushSubscriptionEntityConfiguration.ApplyPushSubscriptionConfiguration` |
| Web Push options | `BlazorBase.Notifications.Server.Configuration.WebPushOptions` (+ `WebPushOptionsValidator`, `PushUrgency`) |
| Web Push sender | `BlazorBase.Notifications.Server.Services.IWebPushSender` / `WebPushSender` (on top of the `WebPush` NuGet package) |
| Push notifier | `BlazorBase.Notifications.Server.Services.IPushNotifier` / `PushNotifier` |
| Subscription store | `BlazorBase.Notifications.Server.Services.IPushSubscriptionStore` / `EfPushSubscriptionStore` |
| Host inbox contract | `BlazorBase.Notifications.Server.Services.INotificationInboxStore` (implemented by the host) |
| Acting user id resolution | `BlazorBase.Notifications.Server.Services.INotificationUserIdResolver` / `ClaimsNotificationUserIdResolver` (default, overridable by the host) |
| VAPID key generation | `BlazorBase.Notifications.Server.Services.VapidKeyGenerator` |
| DI registration | `AddBlazorBaseNotificationsServer<TContext>(IConfiguration, string, Action<WebPushOptions>?)` |
| Endpoint mapping | `BlazorBase.Notifications.Server.Endpoints.BlazorBaseNotificationsEndpointMapper.MapBlazorBaseNotifications` |

---

## Setup

### 1. Generate a VAPID key pair once

```csharp
var (publicKey, privateKey) = VapidKeyGenerator.Generate();
```

Store both in the gitignored `appsettings.Development.json` while developing, and in a secret store
in production — never in source control. Generating a new pair invalidates every subscription
created under the old one, so treat it as stable, long-lived configuration, not a value to rotate
casually.

```json
{
  "BlazorBaseNotifications": {
    "WebPush": {
      "Subject": "mailto:support@example.com",
      "PublicKey": "<VAPID public key>",
      "PrivateKey": "<VAPID private key>"
    }
  }
}
```

`Subject` must be a `mailto:` address or an `https://` URL. `WebPushOptions` is validated on start
(`ValidateOnStart()`); a missing or malformed value fails fast with a message naming the exact
setting, instead of failing lazily on the first push.

### 2. Add the `PushSubscription` entity to the host `DbContext`

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyPushSubscriptionConfiguration();
}
```

Add a `DbSet<PushSubscription>` property and generate a migration the normal way; the framework
owns no migrations itself (the same convention as every other BlazorBase library with server-side
storage).

### 3. Implement `INotificationInboxStore` against the host's own notification model

The framework does not own the host's notification entity, translations, or user model, so the host
implements the four-method contract against its own schema:

```csharp
public class NotificationInboxStore(AppDbContext dbContext) : INotificationInboxStore
{
    public async Task<IReadOnlyList<InboxNotificationDto>> GetUnreadAsync(Guid userId, CancellationToken cancellationToken = default) { /* ... */ }
    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default) { /* ... */ }
    public async Task MarkReadAsync(Guid userId, long id, CancellationToken cancellationToken = default) { /* ... */ }
    public async Task DismissAsync(Guid userId, long id, CancellationToken cancellationToken = default) { /* ... */ }
}
```

### 4. Register the server module and map the endpoints

```csharp
// Server Program.cs
builder.Services.AddDbContext<AppDbContext>(/* ... */);
builder.Services.AddBlazorBaseNotificationsServer<AppDbContext>(builder.Configuration);
builder.Services.AddScoped<INotificationInboxStore, NotificationInboxStore>();

app.MapBlazorBaseNotifications(); // defaults to "api/notifications"
```

`AddBlazorBaseNotificationsServer<TContext>` bridges the base `DbContext` to the host's concrete,
scoped registration (the same pattern `BlazorBase.Files.Server` uses), so `EfPushSubscriptionStore`
needs no separate registration, and wires `IWebPushSender`, `IPushNotifier` and the `WebPushOptions`
validator.

### Optional: resolve the acting user id against a different identity than the claims principal

Every endpoint resolves the acting user id through `INotificationUserIdResolver`, not by reading the
`ClaimTypes.NameIdentifier` claim directly. `AddBlazorBaseNotificationsServer` registers a default
implementation, `ClaimsNotificationUserIdResolver`, with `TryAdd` — it parses the
`ClaimTypes.NameIdentifier` claim as a `Guid` and returns it, or `null` if the claim is missing or
not a `Guid`, which the endpoint turns into a 401.

That default is enough as long as the claims principal's `NameIdentifier` already carries the id the
host's own stores (`INotificationInboxStore`, `IPushSubscriptionStore`) use as `userId`. A host that
keeps a **separate identity user id and domain user id** — for example an Entra ID object id in the
claim, and a different primary key on its own `User` table that `PushSubscription` and the inbox
rows actually reference — registers its own resolver **before** calling
`AddBlazorBaseNotificationsServer`, so `TryAdd` keeps it instead of the default:

```csharp
public class DomainUserIdResolver(IdentityUserResolver identityUserResolver) : INotificationUserIdResolver
{
    public async Task<Guid?> ResolveUserIdAsync(ClaimsPrincipal user, CancellationToken cancellationToken) =>
        await identityUserResolver.ResolveDomainUserIdAsync(user, cancellationToken);
}
```

```csharp
// Server Program.cs
builder.Services.AddScoped<INotificationUserIdResolver, DomainUserIdResolver>();
builder.Services.AddBlazorBaseNotificationsServer<AppDbContext>(builder.Configuration);
```

With this in place, `INotificationInboxStore` and `IPushSubscriptionStore` implementations only ever
see the domain user id and never need their own identity-to-domain translation.

### 5. Call `IPushNotifier` when a notification is created

```csharp
await pushNotifier.NotifyUserAsync(targetUserId, new PushPayload(title, shortBody, url, tag));
// or, for a broadcast:
await pushNotifier.NotifyAllAsync(new PushPayload(title, shortBody, url, tag));
```

`IPushNotifier` sends only to `Enabled` subscriptions, removes a subscription the push service
reports as gone (HTTP 404/410), and never lets one failing subscription stop delivery to the rest of
the batch (bounded parallelism, 8 concurrent sends per call).

### 6. Register the client module

```csharp
// Client Program.cs
builder.Services.AddBlazorBaseNotifications(options =>
{
    options.PollInterval = TimeSpan.FromSeconds(60); // default
});

// AddBlazorBaseNotifications does not register the HttpClients behind the two client
// contracts; the host configures them because the base address and auth handler are host concerns:
builder.Services.AddHttpClient<INotificationInboxClient, HttpNotificationInboxClient>(client =>
        client.BaseAddress = new Uri(new Uri(hostBaseAddress), "api/notifications/"))
    .AddHttpMessageHandler<AuthTokenHandler>();
builder.Services.AddHttpClient<IPushSubscriptionClient, HttpPushSubscriptionClient>(client =>
        client.BaseAddress = new Uri(new Uri(hostBaseAddress), "api/notifications/"))
    .AddHttpMessageHandler<AuthTokenHandler>();
```

### 7. Wire the push handler into the host's service worker

The host's own service worker (offline caching, PWA installability — see
`Templates/BlazorBasePwaTemplate`) imports the framework's push handler so a `push` or
`notificationclick` event is handled without the host writing any JS itself:

```js
// service-worker.published.js
self.importScripts('./_content/BlazorBase.Notifications/blazorbase-push-sw.js');
```

### 8. Place the components

```razor
<NotificationBell />
<PushSettingsToggle />
```

Both render through FluentUI and theme CSS custom properties only — no Bootstrap, FontAwesome, or
Blazorise.

---

## Client components

### `NotificationBell`

An icon button (FluentUI `Alert` icon) with an unread-count badge (`FluentBadge`) and a popover
list. Loads the unread list and count on first render, then keeps itself current three ways:

1. Polling every `NotificationClientOptions.PollInterval` (default 60 s), via `TimeProvider.CreateTimer`
   so the interval is deterministically testable.
2. On tab focus (`document.visibilitychange` to `visible`), through `INotificationRefreshInterop`.
3. On a message the push service worker posts to open clients right after it shows a native
   notification (`blazorbase-push-sw.js` → `postMessage({ type: 'blazorbase-notification' })`), so an
   open tab reflects a push almost immediately instead of waiting for the next poll.

Each list item can be marked read or dismissed; both actions round-trip through
`INotificationInboxClient` and then refresh the list. The message body renders through
`BlazorBase.Components.Html.SanitizedHtml`, so it is safe to display as HTML (sanitized when an
`IHtmlSanitizer` is registered, HTML-encoded as plain text otherwise — never raw markup either way).

### `PushSettingsToggle`

A single `FluentSwitch` with explanatory caption text, in one of four states:

| State | Meaning | Switch |
|---|---|---|
| `Unsupported` | This browser has no Push API / Service Worker / Notifications API | disabled |
| `Denied` | The user previously denied notification permission for this origin | disabled |
| `Disabled` | Supported, not denied, no subscription registered in this browser yet | off |
| `Enabled` | A subscription exists in this browser and is registered with the host | on |

Turning the switch on performs the full round trip: fetch the VAPID public key
(`IPushSubscriptionClient.GetPublicKeyAsync`), subscribe through the browser
(`IWebPushClient.SubscribeAsync` — which also requests notification permission if it has not been
decided yet), then register the resulting subscription with the host
(`IPushSubscriptionClient.SubscribeAsync`). Turning it off unsubscribes the browser and removes the
registration. Permission is requested **only** inside this click handler, never on page load, so the
browser always sees it as triggered by the user gesture that caused it.

---

## REST endpoints (`MapBlazorBaseNotifications`, default prefix `api/notifications`)

Every endpoint requires an authenticated caller; the acting user id always comes from
`INotificationUserIdResolver` (by default, the `ClaimTypes.NameIdentifier` claim), never from the
request body — a caller can only ever act on their own notifications and subscriptions.

| Method | Route | Body | Success | Notes |
|---|---|---|---|---|
| `GET` | `push/public-key` | — | `PushPublicKeyResponse` | the VAPID public key only, never the private key |
| `POST` | `push/subscription` | `PushSubscriptionDto` | 204 | upsert by endpoint; re-subscribing from the same browser moves ownership to the calling user |
| `DELETE` | `push/subscription?endpoint=` | — | 204 / 404 | only the caller's own subscription |
| `PUT` | `push/subscription/enabled` | `SetPushSubscriptionEnabledRequest` | 204 / 404 | pauses/resumes delivery without removing the subscription |
| `GET` | `unread` | — | `InboxNotificationDto[]` | newest first |
| `GET` | `unread/count` | — | `int` | |
| `POST` | `{id}/read` | — | 204 | |
| `POST` | `{id}/dismiss` | — | 204 | |

---

## Browser / OS limits and privacy

- Web Push requires HTTPS (or `localhost`) and an active service worker registration; there is no
  fallback for browsers or contexts without both.
- Permission can only be requested from inside a user gesture; browsers silently ignore or auto-deny
  a request made on page load. `PushSettingsToggle` only ever asks from its own click handler.
- Once a user denies permission, the browser gives no API to ask again — the only way back is the
  user changing the per-site permission in their own browser settings. `PushSettingsToggle`'s denied
  state explains this instead of showing a button that cannot do anything.
- Windows' Focus Assist, and each OS/browser's own batching of low-urgency pushes, can delay or
  suppress delivery outside the app's control; `WebPushOptions.Urgency` is a hint, not a guarantee.
- Whether a closed browser still shows a toast depends on the OS and browser (Edge/Chrome keep their
  background service running for push on Windows; this is **not** testable by automation — see
  "What only a human can verify" in the phase plan).
- Push payloads carry only `title`, `body`, `url` and `tag`. Nothing sensitive crosses the push
  service; the app fetches full detail after the user clicks through to it.

---

## Project dependencies

```
BlazorBase.Notifications
  └─ BlazorBase.Components   (SanitizedHtml)

BlazorBase.Notifications.Server
  ├─ BlazorBase.Notifications   (shared DTOs)
  └─ BlazorBase.CRUD            (AuditModel base class only; subscriptions are not exposed
                                  through the generic CRUD endpoints)
```
