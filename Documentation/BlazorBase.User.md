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
│   RedirectToLogin   ResetPasswordDialog                      │
├──────────────────────────────────────────────────────────────┤
│  Services                                                    │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ IAuthService    AuthService     HttpClient → /api/auth │  │
│  │ IUserService    UserService     HttpClient → /api/user │  │
│  │ ITokenStorage   (platform impl)                        │  │
│  │ IAppConfigService (platform impl)                      │  │
│  │ ILanguageService LanguageService                       │  │
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
- `AuthTokenHandler` (DelegatingHandler) as scoped
- `ILanguageService` → `LanguageService`
- `IThemeService` → `ThemeService`
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
>   managers store and fill credentials correctly. Note: FluentUI applies the `autocomplete` attribute
>   imperatively via JS interop after render, so it is present once the field has hydrated, not in the
>   very first server-rendered HTML.

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

`BaseLayout` ends with `FluentToastProvider`, `FluentDialogProvider`, `FluentTooltipProvider`,
`FluentMessageBarProvider` and `FluentMenuProvider`. A host that writes its own `MainLayout` instead
**has to repeat them** — `UserManagementView` and every other screen built on `BaseList`/`BaseCard`
throws without the dialog provider. See
[BlazorBase.CRUD](BlazorBase.CRUD.md#6-layout--the-fluent-ui-providers).

---

## Services & Components

### `IAuthService` — `AuthService`

| Method | HTTP | Returns | Purpose |
|---|---|---|---|
| `GetStatusAsync()` | GET `api/auth/status` | `AuthStatusResponse` | First-run probe (`HasUsers`) and whether the server offers development authentication |
| `SetupAsync(SetupRequest)` | POST `api/auth/setup` | `LoginResponse` | Creates first admin and logs in |
| `LoginAsync(LoginRequest)` | POST `api/auth/login` | `LoginResponse` | |
| `RefreshAsync(refreshToken)` | POST `api/auth/refresh` | `LoginResponse` | Single-use rotation — the server revokes the presented token atomically; a replayed/already-used token returns `401` |
| `LogoutAsync(refreshToken)` | POST `api/auth/logout` | — | Revokes the refresh token by its hash; anonymous endpoint, so it succeeds even if the access token has already expired |
| `DevelopmentLoginAsync()` | POST `api/auth/development-login` | `LoginResponse?` | Signs in as the server's development account; `null` when the server does not offer it, which is every environment but Development |

### `IUserService` — `UserService`

| Method | HTTP | Returns | Purpose |
|---|---|---|---|
| `GetMeAsync()` | GET `api/user/me` | `UserProfile` | |
| `UpdateSettingsAsync(req)` | PUT `api/user/me/settings` | — | Theme + language |

### `ITokenStorage`

```csharp
public interface ITokenStorage
{
    Task<LoginResponse?> GetTokensAsync();
    Task SaveTokensAsync(LoginResponse tokens);
    Task ClearTokensAsync();
}
```

Implemented by `BrowserTokenStorage` (WASM, localStorage) and `SecureTokenStorage` (MAUI, `SecureStorage`).

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

> **Still open:** the gate is per Blazor runtime, so two browser tabs of the same app can in principle
> still refresh at the same moment and trip the reuse detection. Closing that needs a cross-tab lock
> (Web Locks) rather than a process-wide semaphore.

### `BlazorBaseUserAuthStateProvider` (AuthenticationStateProvider)

- Reads `AccessToken` from `ITokenStorage` and parses JWT claims via `JwtSecurityTokenHandler`.
- Exposes `MarkUserAsAuthenticated(LoginResponse)` and `MarkUserAsLoggedOut()` for the login/logout flows.

### Pages & Views

| Component | Purpose | Auth |
|---|---|---|
| `Login.razor` (`/login`) | Login + initial admin setup form. Accepts a `[Parameter] bool CollectHouseholdName` (default `false`). When `true` and in setup mode, renders an optional household-name text field bound to `SetupRequest.HouseholdName`. After a successful login, navigates to the `returnUrl` query parameter when present and safe; falls back to `/`. | `[AllowAnonymous]` |
| `SetupServer.razor` (`/setup-server`) | Asks the user for the backend URL (MAUI first-run) | `[AllowAnonymous]` |
| `UserManagementView.razor` | Generic, reusable user CRUD UI (no route, no `[Authorize]`) | host-supplied |

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
- `ResetPasswordDialog` — modal that prompts for a new password and PATCHes `UserModel.Password` through `BaseCard`.

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
| `UserRoleInput` | a `string` property marked `[UserRoleInput]` or named `Role` | a `FluentSelect` over the role names from the app-registered `IUserRoleProvider` |

Both bind a **local string** with plain `@bind-Value`, so the value expression handed to the Fluent UI
input is a clean member access. This is what lets the generic card/list render the user model without the
Fluent UI `FieldIdentifier` "index expression" error that a non-member-access value expression triggers.

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
            await Auth.LogoutAsync(tokens.RefreshToken);

        await AuthState.MarkUserAsLoggedOut();
        Nav.NavigateTo("/login");
    }
}
```

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
        <FluentNavLink Href="/admin/users">User Management</FluentNavLink>
    </Authorized>
</AuthorizeView>
```

---

## Navigation

A data-driven primary navigation under `BlazorBase.User.Layout.Navigation`. The host builds **one** list of
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
| `IsDanger` | Renders with a destructive accent (e.g. logout). |
| `OnClick` | Action invoked instead of navigating (e.g. logout). |
| `Children` | A non-empty list turns the entry into a group that reveals its children instead of navigating. |

Role visibility is evaluated once against the cascading `AuthenticationState`. When `RequiredRole` contains
multiple comma-separated names the entry is visible if the user is in **any** of them (OR semantics). Groups
whose children are all hidden are dropped. Browser-/host-only entries are simply omitted by the host when it
builds the list.

```csharp
new() { Href = "/admin", Label = L["Admin"], RequiredRole = "Admin,Supervisor" }
```

### `BaseSideNavigation`

Desktop renderer (Fluent UI nav menu). Groups render as a labelled section with their children beneath.
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
