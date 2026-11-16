# BlazorBase.User

The shared user/authentication Razor Class Library. Provides everything a Blazor host needs to consume a JWT-secured backend: login + setup pages, user-management page, auth state provider, token refresh handler, language service and request/response models.

> Target framework: `net10.0` · Depends on `BlazorBase.CRUD` (used by `UserManagement.razor`) and Fluent UI Blazor.

Two companion libraries provide the *platform-specific glue*:

- [`BlazorBase.User.Wasm`](BlazorBase.User.Wasm.md) — `localStorage` token storage, same-origin server config
- [`BlazorBase.User.Maui`](BlazorBase.User.Maui.md) — `SecureStorage` token storage, configurable server URL

And the backend side:

- [`BlazorBase.User.Server`](BlazorBase.User.Server.md) — Identity, JWT issuance, `AuthController` / `UserController`, admin endpoints

---

## Table of Contents

- [Architecture & Overview](#architecture--overview)
- [Getting Started](#getting-started)
- [Services & Components](#services--components)
- [User menu](#user-menu)
- [Navigation](#navigation)
- [Models / DTOs](#models--dtos)
- [Auth Flow](#auth-flow)
- [Code Examples](#code-examples)

---

## Architecture & Overview

```
┌──────────────────────────────────────────────────────────────┐
│  Pages (Razor)                                               │
│   /login         Login.razor       (login + first-time setup)│
│   /setup-server  SetupServer.razor (MAUI URL config)         │
│  UserManagementView.razor (generic user CRUD; host adds route)│
├──────────────────────────────────────────────────────────────┤
│  Components                                                  │
│   RedirectToLogin   ResetPasswordDialog   BaseUserMenu       │
├──────────────────────────────────────────────────────────────┤
│  Services                                                    │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ IAuthService    AuthService     HttpClient → /api/auth │  │
│  │ IUserService    UserService     HttpClient → /api/user │  │
│  │ ITokenStorage   (platform impl)                        │  │
│  │ IAppConfigService (platform impl)                      │  │
│  │ ILanguageService LanguageService                       │  │
│  │ IUserSignOutService UserSignOutService                 │  │
│  │ AuthTokenHandler        : DelegatingHandler            │  │
│  │ BlazorBaseUserAuthState : AuthenticationStateProvider  │  │
│  └────────────────────────────────────────────────────────┘  │
├──────────────────────────────────────────────────────────────┤
│  Platform glue (BlazorBase.User.Wasm / .Maui)                │
│   registers ITokenStorage + IAppConfigService                │
├──────────────────────────────────────────────────────────────┤
│  Backend (BlazorBase.User.Server)                            │
│   /api/auth/{status,setup,login,refresh,logout}              │
│   /api/user/me,  /api/user/me/settings                       │
│   /api/base/users  (Admin CRUD via BlazorBase.CRUD)          │
└──────────────────────────────────────────────────────────────┘
```

**Design notes**
- All client-server communication is JSON over `HttpClient`. There is no SignalR or shared-state dependency.
- The library is host-agnostic: it never assumes WASM or MAUI, only that `ITokenStorage` + `IAppConfigService` are registered.
- `IUserService` and `IAuthService` are registered with `AddHttpClient<…>()` by the host, because the `HttpClient` base address depends on the host.
- `AuthTokenHandler` is a `DelegatingHandler` — attach it to the typed clients to get automatic bearer + refresh.

---

## Getting Started

### 1. Project reference

```xml
<ProjectReference Include="..\Libs\BlazorBase.User\BlazorBase.User.csproj" />
```

### 2. Register the shared services

```csharp
builder.Services.AddBlazorBaseUserClient();
```

This registers:
- `BlazorBaseUserAuthStateProvider` as the `AuthenticationStateProvider`
- `CsrfTokenProvider` and `AuthenticationSessionState` as scoped (`TryAdd`); the WebAssembly and MAUI
  registrations replace both with one instance per process through
  `AddBlazorBaseUserSharedClientSession()`, because IHttpClientFactory resolves message handlers in
  scopes of their own (see [BlazorBase.User.Wasm](BlazorBase.User.Wasm.md#one-session-per-process-why-the-state-is-not-scoped)).
  The shared session refuses to run in an ASP.NET Core server process, recognized by
  `Microsoft.AspNetCore.Hosting.Server.IServer` being registered in the container: the host fails to
  start and the shared instances cannot be constructed. There is no opt-out. A server-rendered host
  calls `AddBlazorBaseUserClient()` alone, which keeps the state scoped, and registers a scoped
  `ITokenStorage` of its own
- `AuthTokenHandler` and `SessionExpiryHandler` (DelegatingHandlers) as scoped
- `ILanguageService` → `LanguageService`
- `IThemeService` → `ThemeService`
- `IThemePreferenceSync` → `UserThemePreferenceSync` (stores the theme choice of a signed-in user via `PUT api/user/me/settings`; fails soft when offline)
- `IUserLanguagePreferenceSync` → `UserLanguagePreferenceSync` (the same for the language choice of [`BaseUserMenu`](#user-menu); `TryAdd`)
- `IUserSignOutService` → `UserSignOutService` (the framework sign-out path, see [Logout](#logout); `TryAdd`)
- `AuthorizationCore` with a `FallbackPolicy` requiring authenticated users
- `BlazorBaseUserClientOptions` with default values (all flags off)

> **The `FallbackPolicy` does not protect routable pages.** `AuthorizeRouteView` only
> evaluates the `[Authorize]` attributes it finds on the page component; a page without one
> renders for anonymous visitors regardless of the fallback policy, and its API calls then
> fail with `401`. Put `@attribute [Authorize]` on every page that needs a signed-in user
> (or once in the host's `_Imports.razor`), and mark the deliberate exceptions
> `[AllowAnonymous]`. The fallback policy still applies where a policy is resolved without
> explicit data, such as `<AuthorizeView>` without `Roles`/`Policy`.

To opt in to collecting a household/group name during first-run setup:

```csharp
builder.Services.Configure<BlazorBaseUserClientOptions>(o =>
    o.CollectHouseholdNameOnSetup = true);
```

The other options: `ShowForgotPasswordLink` (default `false`) adds a link to `/forgot-password` on the login
page - see [Password reset pages](#password-reset-pages); `OAuthEndpointBasePath` (default `api/oauth`) and
`InvitationEndpointBasePath` (default `api/auth/invitations`) must match the server's paths.

### 3. Register platform-specific services

These supply the three platform seams — `ITokenStorage`, `IAppConfigService` and
`IFormFactor` — that the shared components resolve. Skipping this step makes
`UserPreferencesPanel` throw when it renders.

WebAssembly:
```csharp
builder.Services.AddBlazorBaseUserWasm(builder.HostEnvironment.BaseAddress);
```

MAUI:
```csharp
builder.Services.AddBlazorBaseUserMaui();
```

### 4. Register the typed HTTP clients

```csharp
// The AuthTokenHandler injects IAuthService, so attaching it to the auth client itself
// closes a dependency cycle. The auth endpoints are anonymous and need no bearer.
builder.Services.AddHttpClient<IAuthService, AuthService>(c => c.BaseAddress = baseUri);

builder.Services.AddHttpClient<IUserService, UserService>(c => c.BaseAddress = baseUri)
    .AddHttpMessageHandler<AuthTokenHandler>();
```

`baseUri` is the API root (`https://api.example.com/`). The services post to relative paths like `api/auth/login`.

`GET api/auth/session` is the one auth endpoint that does need to know who is calling, and it cannot
learn that from a handler this client does not have. `BlazorBaseUserAuthStateProvider` therefore reads
the access token from `ITokenStorage` and passes it to `IAuthService.GetSessionAsync(accessToken)`
itself. Under the cookie transport `ITokenStorage` is `NoOpTokenStorage`, no header is sent, and the
session cookie identifies the caller as before. Nothing here changes for the host — but a host that
replaces `IAuthService` must honour that parameter, or its client will report every user as signed
out while the server happily authorises the same requests.

### 5. Optional — skip the login form while developing

```csharp
var host = builder.Build();
await host.Services.UseBlazorBaseDevelopmentSessionAsync();
await host.RunAsync();
```

Asks the server whether it offers development authentication and, if so, signs in as its
configured account before the first render — so a developer lands on the app instead of the login
form. Against any other server the call does nothing, so it is safe to leave in place; the server
side and its guards are described under
[DevelopmentAuthentication](BlazorBase.User.Server.md#configuration-developmentauthentication).

It signs in on every start rather than reusing a stored token, because a token minted before the
roles changed in configuration would keep the old ones and quietly defeat the point of switching
them.

It works under both transports. Under the cookie transport the development login is a POST like any
other and needs the antiforgery token, which nothing has fetched that early, so the call reads an
anonymous session first. The sign-in itself goes through
`BlazorBaseUserAuthStateProvider.MarkUserAsAuthenticated`, exactly like the login page, which also
swaps the anonymous antiforgery token for one bound to the new identity.

### 6. Wire the pages

`BlazorBase.User` ships `/login`, `/setup-server` and `/admin/users` pages. The host's router (`Routes.razor`) automatically picks them up because the library is referenced.

> **Security hardening (work item #112).**
> - **USRV-06:** `UserModel` now carries a class-level `[CrudAccess("Admin", "RIMD")]`, so the generic
>   CRUD engine restricts the user entity to the `Admin` role even if a host maps its endpoints via a
>   generic assembly scan (`MapBlazorBaseCrudEndpoints(assembly)` with no role) rather than
>   `MapBlazorBaseUserAdminEndpoints("Admin")`. Defense-in-depth against a mis-wired scan exposing the
>   user model role-lessly.
>
>   **⚠️ Breaking change** for a host that maps user administration to a role **other than `Admin`**
>   (e.g. `MapBlazorBaseUserAdminEndpoints(roles: "Verwalter")`): the class rule intersects with the
>   endpoint role gate, so a principal in that non-`Admin` role now gets `403 Forbidden` on every user
>   operation (and the in-app `UserManagementView` renders empty) — fail-closed, never over-exposing.
>   **Remediation:** treat "Admin" as the framework's canonical user-administration role and grant it to
>   your user administrators; or, if you need a different role name, subclass `UserModel` with your own
>   `[BaseCrud(...)]` route and a matching `[CrudAccess("<your-role>", "RIMD")]` and map that instead of
>   the framework model (`[CrudAccess]` rules union per level, so an added rule grants the extra role).
> - **USER-02:** the login and first-run setup fields in `Login.razor` carry `autocomplete` hints
>   (`username`, `current-password`, `new-password`, `name`; HouseholdName uses `off`) so browser password
>   managers store and fill credentials correctly. The hint is set through the `AutoComplete` parameter of
>   `FluentTextInput`.

Use `<AuthorizeRouteView>` so unauthenticated users land on `/login`:

```razor
<Router AppAssembly="@typeof(App).Assembly" AdditionalAssemblies="new[]{ typeof(Login).Assembly }">
    <Found Context="routeData">
        <AuthorizeRouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)">
            <NotAuthorized>
                <RedirectToLogin />
            </NotAuthorized>
        </AuthorizeRouteView>
    </Found>
</Router>
```

### 6. Initialize language on startup

In `MainLayout.razor.cs`:
```csharp
protected override async Task OnInitializedAsync()
{
    await LanguageService.InitializeAsync();
}
```

### 7. Carry the Fluent UI providers in your own layout

`BaseLayout` ends with `<FluentProviders />`, which carries the dialog, toast, tooltip and keyboard providers. A host
that writes its own `MainLayout` instead **has to add it** and register the services with `AddBlazorBaseFluentUI()` —
`UserManagementView` and every other screen built on `BaseList`/`BaseCard` throws without the dialog provider. See
[BlazorBase.CRUD](BlazorBase.CRUD.md#6-layout--the-fluent-ui-providers).

---

## Services & Components

### `IAuthService` — `AuthService`

| Method | HTTP | Returns | Purpose |
|---|---|---|---|
| `GetStatusAsync()` | GET `api/auth/status` | `AuthStatusResponse` | First-run probe (`HasUsers`) and whether the server offers development authentication |
| `SetupAsync(SetupRequest)` | POST `api/auth/setup` | `LoginResponse` | Creates first admin and logs in |
| `LoginAsync(LoginRequest)` | POST `api/auth/login` | `AuthServiceLoginOutcome` | A completed login (`Tokens`) or, when the account has two-factor authentication enabled, a pending token (`PendingToken`) to complete with `LoginTwoFactorAsync` — the server answers `202 Accepted` rather than a token pair, so the caller never receives half-authenticated tokens by mistake |
| `LoginTwoFactorAsync(TwoFactorLoginRequest)` | POST `api/auth/login/two-factor` | `LoginResponse` | Completes a login `LoginAsync` reported as `RequiresTwoFactor` |
| `RefreshAsync(refreshToken)` | POST `api/auth/refresh` | `LoginResponse` | Single-use rotation — the server revokes the presented token atomically; a replayed/already-used token returns `401` |
| `LogoutAsync(refreshToken = null)` | POST `api/auth/logout` | — | Revokes the refresh token by its hash; anonymous endpoint, so it succeeds even if the access token has already expired. Under the cookie transport pass no token: the call sends no body and the cookie is the session. Throws `HttpRequestException` for any non-success status |
| `DevelopmentLoginAsync()` | POST `api/auth/development-login` | `LoginResponse?` | Signs in as the server's development account; `null` when the server does not offer it, which is every environment but Development |
| `ChangePasswordAsync(ChangePasswordRequest)` | POST `api/auth/manage/change-password` | `ChangePasswordOutcome` | Changes the signed-in account's own password. A refusal the server explains with a `400` (`current_password_incorrect`, a rejected new password with its reasons per field, `no_password_set`) is returned as a not-succeeded outcome with `Code`, `Message` and `FieldErrors`; `401`, `429`, `5xx` and network errors throw `HttpRequestException`. Attaches the stored access token itself (this client has no `AuthTokenHandler`; under the cookie transport there is none and the cookie plus CSRF header do the work) and stores the replacement token pair of the response, so a bearer client stays signed in |

### `IUserService` — `UserService`

| Method | HTTP | Returns | Purpose |
|---|---|---|---|
| `GetMeAsync()` | GET `api/user/me` | `UserProfile` | |
| `UpdateSettingsAsync(req)` | PUT `api/user/me/settings` | — | Theme + language |

### `IOAuthConsentService` — `OAuthConsentService`

The client of the OAuth authorization server's user-facing API (see
[OAuth consent page and connected apps](#oauth-consent-page-and-connected-apps)). Paths are relative to
`BlazorBaseUserClientOptions.OAuthEndpointBasePath` (default `api/oauth`, must match the server's
`OAuthServer:EndpointBasePath`).

| Method | HTTP | Returns | Purpose |
|---|---|---|---|
| `GetAuthorizationRequestAsync(request)` | GET `api/oauth/authorization-requests?request=…` | `OAuthAuthorizationRequestDetails?` | `null` for an invalid or expired request; a 403 surfaces as `HttpRequestException` with `StatusCode` |
| `SubmitDecisionAsync(decision)` | POST `api/oauth/authorization-requests/decision` | `OAuthAuthorizationDecisionResponse?` | `null` for an invalid or expired request |
| `GetGrantsAsync()` | GET `api/oauth/grants` | `IReadOnlyList<OAuthGrantSummary>` | Connected apps, newest first |
| `RevokeGrantAsync(id)` | DELETE `api/oauth/grants/{id}` | — | Disconnects an app |

### `ITokenStorage`

```csharp
public interface ITokenStorage
{
    Task<LoginResponse?> GetTokensAsync();
    Task SaveTokensAsync(LoginResponse tokens);
    Task ClearTokensAsync();
}
```

Implemented by `InMemoryTokenStorage` — the WASM default, since anything in `localStorage` survives a
reload and is readable by any script that gets injected into the page — `BrowserTokenStorage` (WASM,
`localStorage`, an explicit opt-in set with `AddBlazorBaseUserWasm(..., persistTokensInBrowserStorage: true)`
for a host that has decided that trade-off is acceptable) and `SecureTokenStorage` (MAUI, `SecureStorage`).

### `IAppConfigService`

```csharp
public interface IAppConfigService
{
    Task<string?> GetServerUrlAsync();
    Task SaveServerUrlAsync(string url);
    Task<bool> IsConfiguredAsync();
}
```

Implemented by `WasmAppConfigService` (returns the WASM host's same-origin URL) and `MauiAppConfigService` (stores user-entered URL in `SecureStorage`).

### `ILanguageService` — `LanguageService`

```csharp
public interface ILanguageService
{
    string CurrentLanguage { get; }            // "en" by default
    event Action? LanguageChanged;
    Task SetLanguageAsync(string cultureCode);
    void ApplyStartupLanguage(string cultureCode);   // startup only, no event, no reload
    Task InitializeAsync();                    // reads the "language" JWT claim
}
```

`InitializeAsync()` reads the `language` claim from the authenticated user and applies it to `CultureInfo.DefaultThreadCurrentCulture` + `DefaultThreadCurrentUICulture`.

**Under WebAssembly the language has to be settled before the host runs.** The runtime picks the satellite resource assemblies it downloads *while starting*; a language applied afterwards leaves `IStringLocalizer` on the neutral resources, with no exception and no failed request — a switch that persists correctly and changes nothing on screen. Call `UseBlazorBaseLanguageAsync` in the client's `Program.cs`, after the development session (which carries the language claim while developing) and before `RunAsync()`:

```csharp
var host = builder.Build();
await host.Services.UseBlazorBaseDevelopmentSessionAsync();
await host.Services.UseBlazorBaseLanguageAsync("en", "de");
await host.RunAsync();
```

It resolves the signed-in account's `language` claim, falls back to the client system's own culture when there is none, reduces the result to a supported two-letter code and hands it to `ApplyStartupLanguage`. Anything it cannot resolve is logged and falls back to the first supported language rather than keeping the app from starting.

The host project also needs `<BlazorWebAssemblyLoadAllGlobalizationData>true</BlazorWebAssemblyLoadAllGlobalizationData>`. Without it the runtime loads only the globalization shard for the culture the browser booted with and throws *"Blazor detected a change in the application's culture that is not supported with the current project configuration"* the first time a persisted language is applied.

Because resources that were never fetched cannot appear later, a **runtime** change reloads the page: `SetLanguageAsync` navigates with `forceLoad` whenever the requested language differs from the one the app started in, and `Login` does the same when the account signing in uses a different language. Both are gated on `OperatingSystem.IsBrowser()` and on `ApplyStartupLanguage` having been called, so MAUI hosts — where every satellite assembly is present locally — and hosts that never wired the startup call keep their previous behaviour instead of reloading in a loop.

### `AuthTokenHandler` (DelegatingHandler)

- Attaches `Authorization: Bearer <accessToken>` to every outbound request.
- **Proactively refreshes** the token when it is within 1 minute of expiry (`RefreshThreshold = TimeSpan.FromMinutes(1)`).
- On `401 Unauthorized` it attempts a refresh + retry once.
- Auth endpoints (`/api/auth/login`, `/refresh`, `/status`, `/setup`, `/logout`) are excluded from the proactive refresh and the `401` retry to avoid loops — logout no longer triggers an auto-refresh.

Two properties of the refresh are load-bearing, and both used to end sessions that were perfectly
valid:

- **Only one refresh runs at a time, app-wide.** The gate is a `static SemaphoreSlim`, because a host
  registers this handler once per typed client and an instance field would only serialise one
  client's requests — a page that loads from two of them still refreshed twice. That matters because
  refresh tokens rotate and the server treats a second use of a rotated token as theft: it revokes
  the **whole family**, signing the user out everywhere. Two parallel calls carrying the same token
  were enough. After passing the gate the handler re-reads storage and adopts a renewal another
  request already completed instead of starting its own.
- **A refresh that never got an answer does not end the session.** Only `401`/`403` — the server
  actually turning the token down — clears it. A transport failure leaves the tokens alone and lets
  the original request fail, because otherwise a phone moving between wifi and mobile data loses its
  login for no reason.

**Who handles a 401 under the bearer transport.** When `AuthTokenHandler` and `SessionExpiryHandler` are
chained on the same client, exactly one of them answers a 401, and the chaining order does not matter.
`AuthTokenHandler` marks every request it will recover itself - it holds a refresh token and the request is
not an auth endpoint - through an internal request option, and `SessionExpiryHandler` leaves the 401 of a
marked request alone:

- a 401 recovered by the refresh never announces an anonymous user to the components;
- a refresh the server rejects (`401`/`403`) still signs the user out, through `AuthTokenHandler`;
- a 401 on a request without a held refresh token, or on an auth endpoint, is dropped to anonymous by
  `SessionExpiryHandler` as before;
- under the cookie transport `AuthTokenHandler` is not chained, nothing is marked, and
  `SessionExpiryHandler` behaves exactly as before.

Two behavior changes follow from this and are intended:

- A refresh that fails for network reasons keeps the session and the stored tokens. `SessionExpiryHandler`
  chained next to the handler used to clear the tokens before the refresh ran.
- A retry that still answers `401` after a successful refresh leaves the user signed in. A freshly issued
  token being refused points at the endpoint (another scheme, a misconfiguration), not at the session.

> **Still open:** the gate is per Blazor runtime, so two browser tabs of the same app can in principle
> still refresh at the same moment and trip the reuse detection. Closing that needs a cross-tab lock
> (Web Locks) rather than a process-wide semaphore.

### `BlazorBaseUserAuthStateProvider` (AuthenticationStateProvider)

- Reads the claims from `GET /api/auth/session` (passing the stored access token under the bearer
  transport) and stores the antiforgery token from that answer in `CsrfTokenProvider`.
- Caches the principal in `AuthenticationSessionState` until the next sign-in or sign-out. Every
  provider instance resolving the same state raises `AuthenticationStateChanged` when any of them
  changes it, so a 401 handled by `SessionExpiryHandler` in an IHttpClientFactory handler scope still
  drops the UI to the login screen.
- Exposes `MarkUserAsAuthenticated(LoginResponse)`, `MarkUserAsLoggedOut()` and `InvalidateAsync()` for
  the login/logout flows.
- `InvalidateAsync()` (also what `MarkUserAsLoggedOut()` and a 401 seen by `SessionExpiryHandler` run)
  clears the stored tokens and caches an anonymous principal without taking anything from the server's
  answer. Under the cookie transport, where an antiforgery token is held, it also replaces that token with
  an anonymous one read from `GET /api/auth/session`, so the login form keeps working without a page
  reload; the anonymous principal is announced even when that read throws, and no stale token is kept.
  Without a held token (bearer transport, MAUI) the server is not asked.
- A host that signs out under the cookie transport must call the server-side logout first and
  `MarkUserAsLoggedOut()` afterwards. The antiforgery token is bound to the identity of the request that
  minted it, so a replacement fetched while the session cookie still identifies the user no longer
  validates once the cookie is gone.

### Pages & Views

| Component | Purpose | Auth |
|---|---|---|
| `Login.razor` (`/login`) | Login + initial admin setup form. Accepts a `[Parameter] bool CollectHouseholdName` (default `false`). When `true` and in setup mode, renders an optional household-name text field bound to `SetupRequest.HouseholdName`. After a successful login, navigates to the `returnUrl` query parameter when present and safe; falls back to `/`. | `[AllowAnonymous]` |
| `SetupServer.razor` (`/setup-server`) | Asks the user for the backend URL (MAUI first-run) | `[AllowAnonymous]` |
| `UserManagementView.razor` | Generic, reusable user CRUD UI (no route, no `[Authorize]`) | host-supplied |
| `OAuthConsent.razor` (`/oauth/consent`) | Consent page of the OAuth authorization server — see [OAuth consent page and connected apps](#oauth-consent-page-and-connected-apps) | `[Authorize]` |
| `AcceptInvitation.razor` (`/invitation/accept?token=...`) | Redeems an invitation link: display name and password, then signed in — see [User invitations](#user-invitations) | `[AllowAnonymous]` |
| `InvitationManagementView.razor` | Invitation administration: list, invite, resend, new link, revoke (no route, no `[Authorize]`) — see [User invitations](#user-invitations) | host-supplied |
| `ForgotPassword.razor` (`/forgot-password`) | Requests a password reset link — see [Password reset pages](#password-reset-pages) | `[AllowAnonymous]` |
| `ResetPassword.razor` (`/reset-password?email=...&token=...`) | Sets the new password from the reset link — see [Password reset pages](#password-reset-pages) | `[AllowAnonymous]` |
| `ChangePassword.razor` (`/account/password`) | The signed-in user changes their own password — see [Change password page](#change-password-page) | `[Authorize]` |

#### `Login` — `CollectHouseholdName` opt-in

There are two ways to activate the household-name field on the lib-routed `/login` page:

**Option A — component parameter** (direct embedding):

```razor
<Login CollectHouseholdName="true" />
```

**Option B — client options** (preferred for the lib-routed `/login`):

```csharp
builder.Services.Configure<BlazorBaseUserClientOptions>(o =>
    o.CollectHouseholdNameOnSetup = true);
```

`CollectHouseholdNameOnSetup` on `BlazorBaseUserClientOptions` is read in `OnInitializedAsync` when the `CollectHouseholdName` parameter was not explicitly set to `true` by the host. This lets any host opt in via DI without needing to override the lib route.

The field is optional — the user may leave it blank. The value travels in `SetupRequest.HouseholdName` to the server's `POST /api/auth/setup` endpoint, where the host's `AuthController` override can read it in `OnAfterUserCreatedAsync`. When neither opt-in path is used, the field is not rendered and `HouseholdName` is `null`; existing hosts are unaffected.

#### Development login — provisioning host-specific data on first use

`POST api/auth/development-login` resolves (creating on first call, reusing afterwards) the account
configured under `DevelopmentAuthentication` and signs it in; a deactivated account (`IsActive == false`)
is refused with `401`, like the password login and the token refresh. A host that provisions additional data by
email on real sign-in — a separate domain user row created from `IExternalUserResolver<TUser>` on first
external sign-in, for instance — needs the same thing to happen for a development account, or it 404s
on any endpoint that reads that other table. Override
`AuthControllerBase<TUser>.OnAfterDevelopmentLoginAsync(user)`, called every time the development
account is resolved (not only when it is first created), to run that provisioning:

```csharp
public class AuthController(..., IDomainUserProvisioningService provisioning)
    : AuthControllerBase<AppUser>(...)
{
    protected override Task OnAfterDevelopmentLoginAsync(AppUser user) =>
        provisioning.EnsureDomainUserAsync(user.Email!, user.DisplayName);
}
```

#### `Login` — `returnUrl` round-trip

After a successful **login**, `Login` reads the `returnUrl` query parameter (URL-encoded, set by `RedirectToLogin`), validates it is app-relative (starts with `/`, not an absolute URI), and navigates there. Absolute or missing values fall back to `/`. After a successful **setup** (first-run), the component always navigates to `/` regardless of `returnUrl` — there is no meaningful prior destination for the first user.

`RedirectToLogin` automatically appends `?returnUrl=<encoded-relative-path>` so that protected pages survive an auth redirect:

```
/inventory → (unauthenticated) → /login?returnUrl=%2Finventory → (login) → /inventory
```

#### `UserManagementView` — the framework user-management UI

`UserManagementView` is a **routeless, authorization-agnostic** Razor component that renders the full
user CRUD experience over the generic `BaseList<UserModel>` + `BaseCard<UserModel>`:

- A list with the core user columns — `DisplayName`, `Email`, `Role`, `IsActive`.
- Create / edit / delete through the standard `BaseCard` dialog. The card resolves the
  `Password` and `Role` fields through the registered password/role custom inputs (see below), so a
  blank password keeps the current one and the role renders as a select over the app's roles.
- Its own chrome strings (add-button caption, empty text, column and field labels) are localized
  through `IStringLocalizer<UserManagementView>` (EN + DE co-located resx).

The host app owns the route, layout, authorization and navigation — it hosts the view on a thin page:

```razor
@page "/admin/users"
@attribute [Authorize(Roles = "Admin")]
@using BlazorBase.User.Pages

<UserManagementView />
```

For this to work the host must register the user-management custom inputs and an `IUserRoleProvider`
(see [User-Management Custom Inputs](#user-management-custom-inputs)) and an
`IBaseDataProvider<UserModel>` (the framework HTTP provider against `api/base/users`).

### Components

- `RedirectToLogin` — placed inside `<NotAuthorized>` in `<AuthorizeRouteView>`. Captures the current URI as an app-relative path and navigates to `/login?returnUrl=<encoded>`. If the current path is already `/login` (or starts with `login`), navigates to `/login` without a `returnUrl` to avoid loops.
- `ResetPasswordDialog` — dialog content (`BaseDialogInstance<ResetPasswordModel>`) that prompts for a new password and writes it to `ResetPasswordModel.NewPassword`. Open it with `IDialogService.ShowBaseDialogAsync<ResetPasswordDialog, ResetPasswordModel>(title, model)`; it shows Fluent's OK and Cancel buttons and the result's `Cancelled` tells which one closed it.
- `OAuthConnectedAppsPanel` — lists the apps the signed-in user connected through OAuth and disconnects one after an `IConfirmationService.ConfirmDestructiveAsync` prompt — see below.
- `InviteUserDialog` (namespace `BlazorBase.User.Components.Invitations`) — the invite form of `InvitationManagementView`; creates the invitation itself and closes with the `UserInvitationLinkResponse` — see [User invitations](#user-invitations).
- `BaseUserMenu` (namespace `BlazorBase.User.Components.UserMenu`) — the account menu for the header: avatar button, name and account, language and theme choice, host entries, "Change password" and "Sign out" — see [User menu](#user-menu).

### OAuth consent page and connected apps

The client half of the [OAuth 2.1 authorization server](BlazorBase.User.Server.OAuth.md) that lets an
access-token-protected endpoint (an MCP server) be added as a claude.ai custom connector.

**`OAuthConsent` (`/oauth/consent`, `[Authorize]`).** `api/oauth/authorize` redirects the browser here
with a sealed `request` query parameter. The page shows the client name **and** the redirect host the
browser will be sent to — the name is chosen by whoever registered the client, the host is the one fact
the user can check; for a client identified by a Client ID Metadata Document also the host that
published it (`ClientIdHost`) — plus the requested scopes with their descriptions (a localized `Scope_{name}`
resource wins over the server-configured description; `Scope_mcp` ships in English and German). *Allow*
and *Deny* post the decision and then leave the application with `NavigateTo(redirectUri, forceLoad: true)`.
An invalid or expired request, or an account outside `OAuthServer:RequiredRoles`, shows a message and no
buttons. An unauthenticated visitor is sent by the host's `RedirectToLogin` to
`/login?returnUrl=%2Foauth%2Fconsent%3Frequest%3D…`; `Login` accepts that local return URL and brings the
user back after signing in.

**`OAuthConnectedAppsPanel`.** A routeless component for the host's settings page: client name, redirect
host, scope, connected and last-used dates per grant, and a *Disconnect* button that revokes the grant
after confirmation — every token the app holds stops working on its next request. Requires
`IConfirmationService` (registered by `AddBlazorBaseCrudComponents()`).

```razor
@page "/settings/connected-apps"
@attribute [Authorize]

<OAuthConnectedAppsPanel />
```

**Registration.** Both use `IOAuthConsentService`, which calls authenticated endpoints, so register it
exactly like `IUserService` — the framework cannot know which handlers the host's clients use:

```csharp
// Bearer transport (AddBlazorBaseUserWasm, MAUI)
builder.Services.AddHttpClient<IOAuthConsentService, OAuthConsentService>(c => c.BaseAddress = apiBase)
    .AddHttpMessageHandler<AuthTokenHandler>();

// Cookie transport (AddBlazorBaseUserWasmCookieAuth)
builder.Services.AddHttpClient<IOAuthConsentService, OAuthConsentService>(c => c.BaseAddress = apiBase)
    .AddBlazorBaseCookieSessionHandlers();
```

A Blazor Server host registers it with whatever authenticated `HttpClient` setup its `IUserService` uses.
Change `BlazorBaseUserClientOptions.OAuthEndpointBasePath` only together with the server's
`OAuthServer:EndpointBasePath`. In a PWA keep both under `api/`: the service worker answers every other
navigation with `index.html`, and `authorize` is reached by a browser navigation.

The consent page is always entered by a full page load (the redirect from `authorize`). Under the cookie
transport the session survives it; under the bearer transport with the default in-memory token storage
it does not, so the user signs in on every connection attempt - one more reason the cookie transport is
the recommended mode for a browser host.

### User invitations

The client half of the [invitation flow](BlazorBase.User.Server.md#user-invitations): an administrator
invites a person by email, the person opens the link, chooses a display name and a password and is signed
in.

**`AcceptInvitation` (`/invitation/accept?token=...`, `[AllowAnonymous]`).** The page the invitation email
links to. It loads the preview (`GET api/auth/invitations/accept`), shows the invited email address,
prefills the suggested display name and asks for the password twice. Submitting redeems the token, calls
`BlazorBaseUserAuthStateProvider.MarkUserAsAuthenticated` with the returned session (as `Login` does) and
navigates to `/` - with a full page load when the new account's language differs from the current one. An
unknown, expired, revoked or used link shows one message and a link to `/login`; a password the policy
rejects shows the server's reasons; an address that got an account meanwhile shows a sign-in hint.

**`InvitationManagementView` (namespace `BlazorBase.User.Pages`, no route).** Routeless like
`UserManagementView`; the host places it on an administrator page. It lists the invitations (email,
display name, role, status `Pending`/`Accepted`/`Expired`/`Revoked`, validity) and offers, for open and
expired ones, *Send again* (new token, new email), *New link* (new token, no email) and *Revoke* (after
`IConfirmationService.ConfirmDestructiveAsync`). *Invite person* opens `InviteUserDialog`
(`BlazorBase.User.Components.Invitations`): email, optional display name, role from the registered
`IUserRoleProvider`, plus the application's own fields. After each create, resend or new link the view shows
the link (made absolute with `NavigationManager.ToAbsoluteUri`) with a *Copy link* button
(`navigator.clipboard.writeText`) and either "The email was sent to ..." or a warning that no email was
sent and the link has to be passed on by hand. Every new link invalidates the previous one.

| Parameter | Meaning |
|---|---|
| `ContextContent` (`RenderFragment<CreateUserInvitationRequest>?`) | application fields rendered in the invite dialog; they set `request.Context` |
| `DefaultRole` (`string?`) | role preselected in the dialog; the first offered role when empty |
| `Language` (`string?`) | language of the invitation emails and of the new accounts; the user type's default when empty |

```razor
@page "/admin/invitations"
@attribute [Authorize(Roles = "Admin")]

<InvitationManagementView DefaultRole="User" Language="de" />
```

**Registration.** `AddBlazorBaseUserInvitationClient` registers `IUserInvitationService` as a typed client
and returns its `IHttpClientBuilder`; add the handlers of the transport exactly like for `IUserService`. The
anonymous accept call needs the antiforgery header under the cookie transport, which
`AddBlazorBaseCookieSessionHandlers()` adds.

```csharp
// Cookie transport (AddBlazorBaseUserWasmCookieAuth)
builder.Services.AddBlazorBaseUserInvitationClient(client => client.BaseAddress = apiBase)
    .AddBlazorBaseCookieSessionHandlers();

// Bearer transport (AddBlazorBaseUserWasm, MAUI)
builder.Services.AddBlazorBaseUserInvitationClient(client => client.BaseAddress = apiBase)
    .AddHttpMessageHandler<AuthTokenHandler>();
```

`IUserInvitationService` members: `GetInvitationsAsync`, `CreateAsync`, `ResendAsync`, `RenewLinkAsync`,
`RevokeAsync`, `GetPreviewAsync`, `AcceptAsync`. A refusal of the server throws `UserInvitationException`
(an `HttpRequestException` with `ErrorCode` from `UserInvitationErrorCodes` and the flattened field
`Errors`); other failures stay plain `HttpRequestException`s. Change
`BlazorBaseUserClientOptions.InvitationEndpointBasePath` (default `api/auth/invitations`) only together
with the path given to `MapBlazorBaseUserInvitationEndpoints`.

### Password reset pages

**`ForgotPassword` (`/forgot-password`, `[AllowAnonymous]`).** Asks for the email address and calls
`IAuthService.ForgotPasswordAsync`; it confirms in neutral words whether or not the address has an
account, matching the server's anti-enumeration answer.

**`ResetPassword` (`/reset-password?email=...&token=...`, `[AllowAnonymous]`).** The page the password
reset email links to (the link `MapBlazorBaseAccountEndpoints` builds from
`AccountManagement:ClientBaseUrl`). It asks for the new password twice and calls
`IAuthService.ResetPasswordAsync`; a refusal shows the server's reason (`ResetPasswordAsync` now throws an
`HttpRequestException` whose message joins the server's `errors`). A link without email or token shows a
message instead of the form.

Both need the server's `MapBlazorBaseAccountEndpoints<TUser>()` and, to actually send mail,
`AddBlazorBaseMailing` plus `AddBlazorBaseMailingEmailSender<TUser>()`. The login page links to
`/forgot-password` only when the host turns it on:

```csharp
builder.Services.Configure<BlazorBaseUserClientOptions>(options => options.ShowForgotPasswordLink = true);
```

### Change password page

**`ChangePassword` (`/account/password`, `[Authorize]`).** The authenticated counterpart of the reset pages: the
signed-in user enters the current password and the new one twice (each field has a show/hide toggle) and the page
calls `IAuthService.ChangePasswordAsync`. It needs the server's `MapBlazorBaseAccountEndpoints<TUser>()` (the
`change-password` route, see [BlazorBase.User.Server](BlazorBase.User.Server.md#account-management--apiauthmanage)) and
the auth client registered as for the other pages (cookie transport: the auth client carries
`CsrfHeaderHandler`; no further registration is needed).

- **Inline validation.** A missing current or new password and a confirmation that differs from the new password are
  reported next to the field without a request (the mismatch while typing). The page does not hard-code a length: the
  server's Identity password rules decide, and their reasons are shown under the new-password field.
- **Server refusals map to fields.** `current_password_incorrect` is shown under the current-password field (in the
  page's own localized text), the reasons of a rejected new password under the new-password field, `no_password_set`
  above the button ("use Forgot your password? on the sign-in page"). `401` shows "session expired", `429` "too many
  attempts", anything else a generic message.
- **Success.** The form is replaced by a confirmation and the user stays signed in: under the cookie transport the
  server has rewritten the session cookie, under the bearer transport `AuthService` stored the replacement token
  pair. Every other session of the account is ended by the server.
- **Reaching it.** `BaseUserMenu` links to it by default (`ShowChangePassword`, `ChangePasswordPath`, see
  [User menu](#user-menu)); a host without passwords (external sign-in only) hides that entry.

---

## Models / DTOs

All under `BlazorBase.User.Models`.

### `AuthStatusResponse`
```csharp
public class AuthStatusResponse
{
    public bool HasUsers { get; set; }
}
```

### `LoginRequest`
```csharp
[Required, EmailAddress] public string Email { get; set; }
[Required]               public string Password { get; set; }
```

### `SetupRequest`
```csharp
[Required, EmailAddress]            public string Email { get; set; }
[Required, MinLength(8)]            public string Password { get; set; }
[Required, MaxLength(100)]          public string DisplayName { get; set; }
[MaxLength(100)]                    public string? HouseholdName { get; set; }   // optional; non-tenant hosts leave it null
```

`HouseholdName` is a generic, optional field. Non-tenant hosts ignore it; a multi-tenant host reads it in its `OnAfterUserCreatedAsync` override to create the first household.

### `LoginResponse`
```csharp
public string AccessToken  { get; set; }
public string RefreshToken { get; set; }
public DateTime ExpiresAt  { get; set; }
```

### `RefreshTokenRequest`
```csharp
public string RefreshToken { get; set; }
```

### `UserProfile`
```csharp
public string Id { get; set; }
public string Email { get; set; }
public string DisplayName { get; set; }
public string ThemePreference { get; set; } = "System";
public string Language { get; set; } = "de";
public string Role { get; set; } = "";
```

### `UpdateUserSettingsRequest`
```csharp
[RegularExpression("^(System|Light|Dark)$")] public string ThemePreference { get; set; }
[RegularExpression("^(de|en)$")]             public string Language { get; set; }
```

### Invitation DTOs

`CreateUserInvitationRequest` (`Email`, `DisplayName?`, `Role`, `Context?` max 2000, `Language?`),
`UserInvitationSummary` (one listed invitation, never the token), `UserInvitationStatus`
(`Pending`, `Accepted`, `Expired`, `Revoked`), `UserInvitationLinkResponse` (`Invitation`, `Link`,
`EmailSent`), `UserInvitationPreview` (`IsValid`, `Email?`, `DisplayName?`, `ExpiresAt?`),
`AcceptUserInvitationRequest` (`Token`, `DisplayName`, `Password`) and the `UserInvitationErrorCodes`
constants - see the [REST reference](BlazorBase.User.Server.md#user-invitations--apiauthinvitations).

### OAuth consent DTOs

| Type | Members |
|---|---|
| `OAuthAuthorizationRequestDetails` | `ClientName`, `RedirectHost`, `ClientIdHost` (nullable), `Scopes` (`List<OAuthScopeDetails>`) |
| `OAuthScopeDetails` | `Name`, `Description` |
| `OAuthAuthorizationDecisionRequest` | `Request` (the sealed request), `Approved` |
| `OAuthAuthorizationDecisionResponse` | `RedirectUri` (client redirect carrying `code` or `error`, `state`, `iss`) |
| `OAuthGrantSummary` | `Id`, `ClientName`, `RedirectHost`, `Scope`, `CreatedAt`, `LastUsedAt` |

### `UserModel` *(admin CRUD — the framework user model)*

`UserModel` is a first-class CRUD model: it derives from `BlazorBase.CRUD.Core.AuditModel`, implements
`IBaseUser`, and carries `[BaseCrud("users")]` so the generic `BaseList`/`BaseCard` can render and
discover it. The wire shape is unchanged from the original DTO (the audit fields it inherits are extra,
nullable JSON members), so existing consumers and the user admin REST endpoints keep working.

```csharp
[BaseCrud("users")]
public class UserModel : AuditModel, IBaseUser
{
    public virtual string Id { get; set; }
    [Required, MaxLength(100)] public virtual string DisplayName { get; set; }
    [Required, EmailAddress]   public virtual string Email { get; set; }
    [Required, MaxLength(128)]
    [UserRoleInput]            public virtual string Role { get; set; }
    public virtual bool IsActive { get; set; } = true;
    public virtual DateTime CreatedAt { get; set; }     // maps to the user's creation timestamp
    [UserPasswordInput]        public virtual string? Password { get; set; }   // write-only (set/reset)
    // CreatedOn/CreatedBy/ModifiedOn/ModifiedBy inherited from AuditModel
}
```

Members are `virtual` so an app can subclass `UserModel` to add its own properties; the subclass is
rendered by the same generic UI and travels through the same data provider.

### `IBaseUser`

The generic contract the user CRUD UI and data provider operate on. Implemented by `UserModel` and any
app subclass.

```csharp
public interface IBaseUser
{
    string Id { get; set; }
    string DisplayName { get; set; }
    string Email { get; set; }
    string Role { get; set; }
    bool IsActive { get; set; }
    string? Password { get; set; }   // write-only: blank keeps, non-blank sets/resets
}
```

---

## User-Management Custom Inputs

`BlazorBase.User` ships two registrable custom property components (built on the
`IBaseCustomPropertyInput` seam from `BlazorBase.CRUD`) so the generic `BaseCard`/`BaseList` renders the
special user fields cleanly:

| Component | Opts in for | Renders |
|---|---|---|
| `UserPasswordInput` | a `string` property marked `[UserPasswordInput]` or named `Password` | a `type=password` box that starts blank in edit mode; only a non-blank entry is pushed back to the model, so leaving it blank keeps the current password |
| `UserRoleInput` | a `string` property marked `[UserRoleInput]` or named `Role` | a `FluentSelect` over the role names from the app-registered `IUserRoleProvider`, with an empty entry first while no offered role is selected |

Neither binds the model property directly: the password input binds a local string, the role select reads the `Value`
parameter and reports changes through `ValueChanged`. So the Fluent UI input never gets a non-member-access value
expression, which is what lets the generic card/list render the user model without the `FieldIdentifier` "index
expression" error that such an expression triggers.

### `IUserRoleProvider`

Roles are app-specific, so the framework only consumes them through a seam the app implements:

```csharp
public interface IUserRoleProvider
{
    IReadOnlyList<string> GetRoleNames();
}
```

### Registration

```csharp
// Registers the password + role custom inputs with the generic CRUD card/list.
builder.Services.AddBlazorBaseUserManagementInputs();

// Supply the role names (kept separate so the role set stays an explicit app concern).
builder.Services.AddBlazorBaseUserRoleProvider<MyRoleProvider>();
```

Without a registered `IUserRoleProvider`, the role select simply renders no options. The custom-input
registration is purely additive — nothing changes for a consumer that does not call it.

---

## Auth Flow

```
┌──────────┐                                ┌──────────────────┐
│  App     │  GET /api/auth/status          │  Server          │
│ startup  │ ─────────────────────────────► │                  │
│          │  ◄───── HasUsers=false ─────── │                  │
│          │                                │                  │
│          │  POST /api/auth/setup          │                  │
│          │   { email, password, name }    │                  │
│          │ ─────────────────────────────► │  Creates admin   │
│          │  ◄── LoginResponse ─────────── │  Issues tokens   │
│          │                                │                  │
│          │  ... user navigates app ...    │                  │
│          │                                │                  │
│          │  HttpClient request            │                  │
│          │   (Authorization: Bearer …)    │                  │
│          │ ─────────────────────────────► │                  │
│          │     ◄── 401 (expired) ──       │                  │
│          │  POST /api/auth/refresh        │                  │
│          │ ─────────────────────────────► │  Issues new      │
│          │  ◄── new LoginResponse ─────── │  Revokes old RT  │
│          │  retry original request        │                  │
│          │ ─────────────────────────────► │  200             │
└──────────┘                                └──────────────────┘
```

Steps:
1. On startup: `/api/auth/status`. If `HasUsers == false`, the `/login` page renders setup mode.
2. **Login** or **setup** returns `LoginResponse`. `BlazorBaseUserAuthStateProvider.MarkUserAsAuthenticated` saves tokens and notifies subscribers.
3. **Token refresh** is automatic: `AuthTokenHandler` refreshes when `ExpiresAt - UtcNow < 1 minute`, or after a `401`. Refresh rotation is single-use — the server revokes the presented refresh token atomically while issuing the new pair, so a replayed or concurrently-lost refresh token gets a `401` instead of a token pair.
4. **Logout** revokes the refresh token (by its hash) via `/api/auth/logout` and clears `ITokenStorage`. The endpoint is anonymous, so logout still succeeds even if the access token stored locally has already expired.
5. `Routes.razor` uses `<AuthorizeRouteView>`; non-authenticated users hit `RedirectToLogin`.

### Resolving an external identity to a local account

`AddBlazorBaseExternalAuth` validates an externally issued OIDC token (e.g. Microsoft Entra ID),
but validating a token only proves who the *provider* thinks the caller is - matching that
identity to a local account is a separate, host-specific decision, made by whatever
`IExternalUserResolver<TUser>` is registered.

A host that calls `AddBlazorBaseUserServer<TUser, TContext>` gets a safe default for free:
`IdentityLoginExternalUserResolver<TUser>` looks the caller up through ASP.NET Core Identity's own
external-login table via `UserManager<TUser>.FindByLoginAsync`, keyed on the caller's subject claim -
never on `email`, `preferred_username`, `upn` or `name`, none of which are stable, provider-issued
identifiers a host can safely trust for account linking. An unrecognised subject is rejected, not
auto-provisioned.

The subject is read under four claim types, in this order of preference: `oid`,
`http://schemas.microsoft.com/identity/claims/objectidentifier`, `sub`, `ClaimTypes.NameIdentifier`.
There are four because the redirect flow's OIDC handler applies inbound claim-type mapping to the raw
JWT claims while token exchange does not, so the same claim arrives under a different name depending
on the entry point. See
[Authentication token transports](BlazorBase.User.Server.AuthTransports.md) for the operational detail
on how the two entry points produce these claim shapes.

Linking an account is therefore something a host must do explicitly, typically once, at invitation
or first-login time:

```csharp
await userManager.AddLoginAsync(user, new UserLoginInfo(
    loginProvider: ExternalAuthSettings.DefaultLoginProvider,
    providerKey: entraObjectId,
    displayName: entraObjectId));
```

A host with a different linking rule - matching by an internal tenant/subject mapping table, for
example - registers its own `IExternalUserResolver<TUser>` (with `AddScoped`, before or after
`AddBlazorBaseUserServer`); that registration always takes precedence over the default. Matching by
`email` alone is an account-takeover vector: an attacker who controls any external identity with a
matching, possibly-unverified email claim would take over the existing account.

---

## Code Examples

### Login from a custom page

```csharp
public partial class LoginPage(IAuthService auth, BlazorBaseUserAuthStateProvider authState, NavigationManager nav)
{
    #region Injects
    private readonly IAuthService Auth = auth;
    private readonly BlazorBaseUserAuthStateProvider AuthState = authState;
    private readonly NavigationManager Nav = nav;
    #endregion

    private async Task LoginAsync(LoginRequest request)
    {
        var tokens = await Auth.LoginAsync(request);
        await AuthState.MarkUserAsAuthenticated(tokens);
        Nav.NavigateTo("/");
    }
}
```

### Logout

`IUserSignOutService.SignOutAsync()` is the framework path and works under both transports (see
[User menu](#user-menu)); `BaseUserMenu` and a bottom-bar entry such as
`new() { Label = L["Logout"], OnClick = SignOutAsync }` can share it:

```csharp
private async Task SignOutAsync()
{
    var result = await SignOutService.SignOutAsync();
    if (result == SignOutResult.Failed)
    {
        ShowSignOutFailedMessage();
        return;
    }

    Nav.NavigateTo("login");
}
```

`SignOutResult.Failed` means the server did not confirm the logout under the cookie transport, so the user is still
signed in and must not be sent to the login page (see [Sign-out per transport](#user-menu)).

What it does, written out by hand for the bearer transport:

```csharp
public partial class TopBar(IAuthService auth, ITokenStorage tokenStorage,
                            BlazorBaseUserAuthStateProvider authState, NavigationManager nav)
{
    #region Injects
    private readonly IAuthService Auth = auth;
    private readonly ITokenStorage TokenStorage = tokenStorage;
    private readonly BlazorBaseUserAuthStateProvider AuthState = authState;
    private readonly NavigationManager Nav = nav;
    #endregion

    private async Task LogoutAsync()
    {
        var tokens = await TokenStorage.GetTokensAsync();
        if (tokens is not null)
        {
            try
            {
                await Auth.LogoutAsync(tokens.RefreshToken);
            }
            catch (HttpRequestException)
            {
            }
        }

        await AuthState.MarkUserAsLoggedOut();
        Nav.NavigateTo("/login");
    }
}
```

Under the cookie transport `tokens` is `null`; `Auth.LogoutAsync()` without a token sends no body and the server ends
the cookie session. Do not sign out locally there when the call throws: the session cookie is still alive, which is
why `IUserSignOutService` returns `Failed` in that case.

### Reading the current user

```csharp
public partial class ProfileBadge(IUserService users)
{
    #region Injects
    private readonly IUserService Users = users;
    #endregion

    private UserProfile? Profile;

    protected override async Task OnInitializedAsync()
        => Profile = await Users.GetMeAsync();
}
```

### Persisting theme + language

```csharp
private async Task SaveSettingsAsync()
{
    await Users.UpdateSettingsAsync(new UpdateUserSettingsRequest
    {
        ThemePreference = SelectedTheme,   // "System" | "Light" | "Dark"
        Language        = SelectedLanguage // "de" | "en"
    });
    await LanguageService.SetLanguageAsync(SelectedLanguage);
}
```

`UpdateSettings` (`PUT api/user/me/settings`) reissues the caller's cookie session via
`SignInManager.RefreshSignInAsync` right after persisting the new values, so the `language`/
`themePreference` claims on the *active* session are current immediately — without this, a user who
changes their language would keep seeing the old one (and `UseBlazorBaseLanguageAsync` would keep
resolving the stale claim) until signing out and back in. Under the bearer-token transport there is no
cookie to rewrite, so this is a no-op from the caller's point of view; the client still has to refresh
its own token pair the way `UserPreferencesPanel.razor.cs` does above.

A host that keeps a separate, host-specific preference store (e.g. a domain user table distinct from
the Identity `AspNetUsers` row) overrides `UserControllerBase<TUser>.OnSettingsUpdatedAsync(user,
request)` — called after the new values are persisted and before the cookie is refreshed — to keep that
store in sync with the same choice:

```csharp
public class UserController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, IDbContextFactory<AppDbContext> dbContextFactory)
    : UserControllerBase<AppUser>(userManager, signInManager)
{
    protected override async Task OnSettingsUpdatedAsync(AppUser user, UpdateUserSettingsRequest request)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var domainUser = await dbContext.Set<DomainUser>().FirstOrDefaultAsync(u => u.Email == user.Email);
        if (domainUser is not null)
        {
            domainUser.PreferredCulture = CultureToLcid(request.Language);
            await dbContext.SaveChangesAsync();
        }
    }
}
```

### Switching language and reapplying culture

```csharp
@implements IDisposable

@code {
    protected override void OnInitialized()
        => LanguageService.LanguageChanged += StateHasChanged;

    public void Dispose()
        => LanguageService.LanguageChanged -= StateHasChanged;
}
```

### Custom AuthorizeView for admin areas

```razor
<AuthorizeView Roles="Admin">
    <Authorized>
        <FluentNavItem Href="/admin/users">User Management</FluentNavItem>
    </Authorized>
</AuthorizeView>
```

---

## User menu

`BaseUserMenu` (namespace `BlazorBase.User.Components.UserMenu`) is the account menu of the header: a 44 px button
with the signed-in user's initials that opens a menu with the user's name and account, the language choice, the theme
choice, optional entries of the host, "Change password" and "Sign out". It lives here rather than in `BlazorBase.Components` because it
signs out, which needs the auth client. Put it into `HeaderEndContent` of `BaseLayout`, so it is the last control of
the header (see the header order in [BlazorBase.Components](BlazorBase.Components.md#baselayout-parameters-and-behavior)):

```razor
@using BlazorBase.User.Components.UserMenu

<BaseLayout AppTitle="Notes" ...>
    <HeaderContent>
        <BaseThemeToggle />
    </HeaderContent>
    <HeaderEndContent>
        <BaseUserMenu Items="UserMenuItems" />
    </HeaderEndContent>
    ...
</BaseLayout>
```

```csharp
private IReadOnlyList<UserMenuItem> UserMenuItems =>
[
    new() { Key = "settings", Label = Localizer["Settings"], Href = "settings", Icon = new Size20.Settings() },
];
```

| Parameter | Default | Purpose |
|---|---|---|
| `ShowLanguage` | `true` | Shows the language choice. It is left out anyway with fewer than two `Languages`. |
| `Languages` | `["en", "de"]` | Two-letter codes of the languages the app ships resources for, in display order. Labels come from the menu's resources (`Language_en` = "English", `Language_de` = "Deutsch"), otherwise from the culture's native name. Pass the same list as to `UseBlazorBaseLanguageAsync`. The account (`UpdateUserSettingsRequest`) only stores `en` and `de`. |
| `ShowTheme` | `true` | Shows System / Light / Dark. Hide it when the host offers the choice elsewhere and wants it only once. |
| `Items` | none | `UserMenuItem` entries (`Key`, `Label`, `Href` or `OnClick`, optional `Icon`) listed above "Sign out", for example "Settings" or "Profile". An entry with `Href` is a link, one with `OnClick` a button; with both, the link wins. |
| `LoginPath` | `login` | Where "Sign out" navigates to, relative to the base address. |
| `ShowChangePassword` | `true` | Shows the built-in "Change password" entry (a link to `ChangePasswordPath`) after the host `Items` and above "Sign out". Hide it for an app whose accounts have no password (external sign-in only) or that offers its own page. |
| `ChangePasswordPath` | `account/password` | Where "Change password" navigates to, relative to the base address; the default is the [`ChangePassword` page](#change-password-page) of this library. |
| `AvatarSrc` | none | A picture shown instead of the initials. |

**What it shows.** Nothing for an anonymous visitor; it follows `AuthenticationStateChanged`, so it disappears after
a sign-out anywhere. The name is the `displayName` claim (falling back to the user name, then the e-mail address); the
second line is the `email` claim (or the user name) when it differs from the name. The initials are the first letters
of the first and the last word of the name, one letter for a single word, `?` without one.

**What the entries do.**

| Entry | Effect |
|---|---|
| Language | `IUserLanguagePreferenceSync.SaveAsync(code)` stores it on the account (`PUT api/user/me/settings`, theme kept; under the bearer transport the token pair is refreshed afterwards so the next session read carries the new claim), then `ILanguageService.SetLanguageAsync(code)`, which reloads the page under WebAssembly. The order matters: the reload resolves the language from the account (`UseBlazorBaseLanguageAsync`), so it has to be stored first. Nothing happens for the current language; an offline user still gets the switch for the session. |
| Theme | `IThemeService.SetThemeAsync(preference)`, which applies it, keeps it in the browser and stores it on the account (`IThemePreferenceSync`). |
| Host entry | Navigates to `Href` or awaits `OnClick`. |
| Change password | A link (`<a role="menuitem" href="{ChangePasswordPath}">`, Blazor intercepts it as a client-side navigation) to the [change password page](#change-password-page); the menu closes after the choice. |
| Sign out | `IUserSignOutService.SignOutAsync()`, then `NavigationManager.NavigateTo(LoginPath)` once the user is signed out; when the server did not confirm the logout (`SignOutResult.Failed`) it shows an alert instead and does not navigate. |

**Sign-out per transport.** `UserSignOutService` posts `api/auth/logout` first and calls
`BlazorBaseUserAuthStateProvider.MarkUserAsLoggedOut()` afterwards, under both transports. The transport is told
apart by what `ITokenStorage` holds: a stored refresh token means bearer, nothing stored (`NoOpTokenStorage`) means
cookie. The server has to confirm the logout with a success status; `IAuthService.LogoutAsync` throws
`HttpRequestException` for any other status (for example `403` for a missing antiforgery token, `429`, `5xx`), and a
network error or timeout counts as unconfirmed too. `SignOutAsync()` returns a `SignOutResult`:

| Transport | Server confirmed | Result | Client state |
|---|---|---|---|
| Bearer | yes | `SignedOut` | Tokens cleared, anonymous. |
| Bearer | no | `SignedOutWithoutServerRevocation` | Tokens cleared, anonymous. The refresh token family may stay valid on the server until it expires. |
| Cookie | yes | `SignedOut` | Anonymous. |
| Cookie | no | `Failed` | Unchanged, still signed in. |

- **Bearer**: the refresh token lives on the client, so dropping it ends the client session even when the server
  could not revoke the family. The tokens are always cleared; the result only reports whether the revocation reached
  the server.
- **Cookie**: the request carries no body; the server acts on the session cookie alone, removes the server-side ticket and
  clears the cookie. `CsrfHeaderHandler` on the auth client sends the antiforgery token. The `HttpOnly` cookie and the
  server session are out of the client's reach, so without the server's confirmation the session stays alive and a
  reload would sign the user straight back in (a risk on a shared device). The client therefore stays signed in and
  returns `Failed` instead of showing an anonymous UI over a live session. Only after a confirmed logout does
  `MarkUserAsLoggedOut()` fetch the anonymous antiforgery token, so the login form works without a reload.
- **Failure handling in `BaseUserMenu`**: `SignedOut` and `SignedOutWithoutServerRevocation` navigate to `LoginPath`
  (the menu disappears with the session, so it shows no notice for the bearer case; a host that wants one calls
  `IUserSignOutService` itself and reads the result). `Failed` keeps the menu open, shows the alert "Signing out
  failed – please try again." (German: "Abmelden hat nicht geklappt – bitte erneut versuchen.", resource
  `SignOutFailed`) above the entry, does not navigate and lets the user retry.

**Keyboard and accessibility.** The button is a native `<button>` with `aria-haspopup="menu"`, `aria-expanded` and,
while open, `aria-controls`; its accessible name is "Account menu of {name}". Enter or Space opens the menu (Arrow Down
opens it at the first entry, Arrow Up at the last), and focus moves to the first entry. The menu is a plain
`role="menu"` region (no `FluentMenu`, so it needs no provider and is bUnit-testable) that is only in the DOM while
open: the name block above it, the language and the theme choice as `role="group"`s of `menuitemradio` entries with
`aria-checked`, host entries, "Change password" and "Sign out" as `menuitem`s, `role="separator"` lines between the sections. Arrow keys,
Home and End move between the entries, Enter or Space activates one (Space also on a link entry), Escape closes the
menu and returns the focus to the button, and so does choosing an entry. A pointer press or the focus moving outside,
Tab out of the menu and any navigation close it without moving the focus. The outside, Escape and arrow handling is a
small WASM-safe module, `_content/BlazorBase.User/js/userMenu.js`, attached only while the menu is open.

**Layout.** Above 768 px the menu is anchored below the button at the end of the header, 300 px wide, and scrolls
when the viewport is short. At 768 px and below it is a bottom sheet with a dimmed backdrop and a grab handle, entries
44 px high (also on any coarse pointer). It is styled with the `--color-*`, spacing and radius tokens only, so it
follows light and dark themes; the button uses `--base-header-control-size` and the selected background while open,
like the plain header icons. All texts are localized in English and German (`BaseUserMenu.razor.resx`).

**Registration and tests.** `AddBlazorBaseUserClient()` registers `IUserSignOutService` and
`IUserLanguagePreferenceSync` (`TryAdd`, so a host can replace either — for example a sign-out that also ends an
external identity provider session). The component additionally needs `ILanguageService`, `IThemeService` (both
registered by `AddBlazorBaseUserClient()`), `NavigationManager` and `IJSRuntime`. A bUnit test of a layout that hosts
it registers stubs for the two services and sets the JS interop to loose mode.

---

## Navigation

A data-driven primary navigation under `BlazorBase.Components.Layout.Navigation`. The host builds **one** list of
`NavigationItem` entries and feeds it to two renderers, so the desktop and mobile presentations cannot
diverge — only the rendering differs.

### `NavigationItem`

An immutable `record` describing one entry:

| Member | Purpose |
|---|---|
| `Href` | Target route for a plain link. Null for groups and actions. |
| `Label` | Already-localized display text (the host localizes; the framework only renders). |
| `Icon` | Optional Fluent UI `Icon`. |
| `MatchAll` | Match the route in full (`NavLinkMatch.All`), e.g. the `/` home link. |
| `RequiredRole` | When set, the entry is only shown to users in any of the listed roles. A single role name or a comma-separated list (e.g. `"Admin,Member"`). `null` means always visible. |
| `IsMobilePrimary` | Whether the entry is a primary tab in the bottom bar; otherwise it lands in the "More" sheet. |
| `BottomBarOnly` | The entry exists for phones only and is never listed in `BaseSideNavigation` on wide screens. It is a bottom bar tab when `IsMobilePrimary` selects it; when it does not fit into the bar (more tabs than `MaxBottomBarItems`/`MaxPrimaryItems`) it is listed in "More" — the built-in sheet of `BaseBottomNavigation`, or the navigation drawer of `BaseLayout` while the bottom bar is shown — so it never vanishes from the phone menu. Default `false`. |
| `IsDanger` | Renders with a destructive accent (e.g. logout). |
| `OnClick` | Action invoked instead of navigating (e.g. logout). |
| `Children` | A non-empty list turns the entry into a group that reveals its children instead of navigating. |
| `ExpandedByDefault` | A group is open until the user opens or closes it once; the remembered choice wins afterwards. Default `false`. |

Role visibility is evaluated once against the cascading `AuthenticationState`. When `RequiredRole` contains
multiple comma-separated names the entry is visible if the user is in **any** of them (OR semantics). Groups
whose children are all hidden are dropped. Browser-/host-only entries are simply omitted by the host when it
builds the list.

```csharp
new() { Href = "/admin", Label = L["Admin"], RequiredRole = "Admin,Supervisor" }
```

### `BaseSideNavigation`

Desktop renderer (Fluent UI `FluentNav`). Groups render as collapsible categories with their children beneath.
Parameters: `Items`, `IsCollapsed`, `Title`, `AriaLabel`.

### `BaseBottomNavigation`

Mobile renderer. `IsMobilePrimary` links become bottom tabs (icon over label, the whole tab is highlighted
when active); secondary links, groups and actions move into a slide-up "More" sheet, where groups drill down
to their children with a back step. The component owns the look of the tabs and sheet; **the host positions
and shows/hides the `.base-bottom-nav` root** (e.g. a fixed bottom bar revealed only on small screens). The
sheet is rendered as a sibling of the bar, so do not wrap the component in a `position: fixed`/transformed
element or its full-screen backdrop will be clipped.

The optional `MoreSheetContent` `RenderFragment` renders host-supplied content at the top of the "More"
sheet root view (above the secondary navigation list) — use it to move richer controls that do not fit a
`NavigationItem`, such as a tenant/household switcher, a theme toggle or a user summary, off the top header
and into the sheet on small screens. Supplying only `MoreSheetContent` (with no secondary items) is enough
to surface the "More" tab.

### Example

```csharp
IReadOnlyList<NavigationItem> items =
[
    new() { Href = "/", MatchAll = true, Label = L["Dashboard"], Icon = new Icons.Regular.Size20.Home(), IsMobilePrimary = true },
    new() { Href = "/work-items", Label = L["WorkItems"], Icon = new Icons.Regular.Size20.TaskListSquareLtr() },
    new()
    {
        Label = L["Admin"], Icon = new Icons.Regular.Size20.Settings(), RequiredRole = "Admin",
        Children = [ new() { Href = "/admin/users", Label = L["Users"], Icon = new Icons.Regular.Size20.People() } ],
    },
    new() { Label = L["Logout"], Icon = new Icons.Regular.Size20.SignOut(), IsDanger = true, OnClick = LogoutAsync },
];
```

```razor
<BaseSideNavigation Items="items" IsCollapsed="collapsed" Title="MyApp" />
@* … and, on mobile … *@
<BaseBottomNavigation Items="items" />
```
