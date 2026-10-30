# BlazorBase.User.Wasm

The Blazor WebAssembly platform implementation of the host-specific seams defined in [`BlazorBase.User`](BlazorBase.User.md). It provides:

- `InMemoryTokenStorage` → `ITokenStorage`, the default; `BrowserTokenStorage` → `ITokenStorage` backed by `localStorage`, an explicit opt-in
- `WasmAppConfigService` → `IAppConfigService` that returns the host's same-origin base address (no user configuration needed)

> Target framework: `net10.0` · References `BlazorBase.User` and `Microsoft.AspNetCore.Components.WebAssembly`.

---

## Table of Contents

- [Architecture & Overview](#architecture--overview)
- [Getting Started](#getting-started)
- [Services](#services)
- [Code Examples](#code-examples)

---

## Architecture & Overview

```
┌────────────────────────────────────────────────────────────┐
│  BlazorBase.User (shared)                                  │
│   ITokenStorage    ←┐                                      │
│   IAppConfigService←┤  abstractions                        │
│                     │                                      │
│  ┌──────────────────┴───────────────────────────────────┐  │
│  │ BlazorBase.User.Wasm                                 │  │
│  │  InMemoryTokenStorage    default (no persistence)    │  │
│  │  BrowserTokenStorage     localStorage, opt-in        │  │
│  │  WasmAppConfigService    host.BaseAddress            │  │
│  │                                                      │  │
│  │  AddBlazorBaseUserWasm(hostBaseAddress,               │  │
│  │                        persistTokensInBrowserStorage) │  │
│  └──────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────┘
```

**Storage key:** `blazorbase_auth_tokens` (in `localStorage`, only when `persistTokensInBrowserStorage`
is set).

WASM hosts always serve the API at the same origin as the WebAssembly bundle, so `IAppConfigService` is effectively a constant — `GetServerUrlAsync()` returns the configured base address, and `IsConfiguredAsync()` always returns `true`.

---

## Getting Started

### 1. Project reference

```xml
<ProjectReference Include="..\Libs\BlazorBase.User.Wasm\BlazorBase.User.Wasm.csproj" />
```

### 2. Register services

```csharp
// Program.cs (WASM)

builder.Services.AddBlazorBaseUserClient();
builder.Services.AddBlazorBaseUserWasm(builder.HostEnvironment.BaseAddress);
```

`builder.HostEnvironment.BaseAddress` is the URL the WASM app was served from. `AddBlazorBaseUserWasm`
registers `ITokenStorage`, `IAppConfigService` and `IFormFactor` as **scoped** services.
`ITokenStorage` defaults to `InMemoryTokenStorage` — a browser reload signs a bearer-mode user out,
which is the correct trade-off for a browser; pass `persistTokensInBrowserStorage: true` to keep the
token pair in `localStorage` across a reload instead, accepting that it becomes readable by any script
that gets injected into the page. The cookie transport
(`AddBlazorBaseUserWasmCookieAuth`, see [BlazorBase.User.Server.AuthTransports](BlazorBase.User.Server.AuthTransports.md))
is the recommended mode for a browser host precisely because it avoids this trade-off altogether.

### 3. Wire HTTP clients with the token handler

```csharp
var apiBase = new Uri(builder.HostEnvironment.BaseAddress);

// No AuthTokenHandler here — see the warning below.
builder.Services.AddHttpClient<IAuthService, AuthService>(c => c.BaseAddress = apiBase);

builder.Services.AddHttpClient<IUserService, UserService>(c => c.BaseAddress = apiBase)
    .AddHttpMessageHandler<AuthTokenHandler>();
```

> **Do not put `AuthTokenHandler` on the `IAuthService` client.** The handler injects
> `IAuthService` to refresh tokens, so attaching it to that very client closes a dependency
> cycle and the first resolve throws `Lazy_Value_RecursiveCallsToValue`. The auth endpoints
> (`status`, `setup`, `login`, `refresh`) are anonymous and need no bearer; `logout` is called
> with the refresh token in the body. Every **other** typed client should carry the handler.

### 4. Optional — register BlazorBase.CRUD client

```csharp
builder.Services.AddBlazorBaseCrud();
builder.Services.AddBlazorBaseCrudComponents();
builder.Services.AddBlazorBaseCrudClient(c => c.BaseAddress = new Uri(apiBase, "api/base/"))
    .AddHttpMessageHandler<AuthTokenHandler>();
```

> Note: `AddBlazorBaseCrudClient` internally calls `AddHttpClient(…)` per entity, so chaining `AddHttpMessageHandler<AuthTokenHandler>()` on its returned builder applies the handler to all CRUD clients.
>
> `AddBlazorBaseCrudComponents()` registers the interactive UI services the CRUD components depend on (`IConfirmationService`) and must be called after `AddBlazorBaseFluentUI()`.

---

## Services

### `BrowserTokenStorage : ITokenStorage`

An explicit opt-in (`AddBlazorBaseUserWasm(hostBaseAddress, persistTokensInBrowserStorage: true)`) over
the `InMemoryTokenStorage` default. Stores the `LoginResponse` (access token, refresh token,
`ExpiresAt`) as JSON in `localStorage` under `blazorbase_auth_tokens`.

```csharp
public async Task<LoginResponse?> GetTokensAsync()
{
    var json = await JsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
    return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<LoginResponse>(json);
}

public async Task SaveTokensAsync(LoginResponse tokens)
    => await JsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(tokens));

public async Task ClearTokensAsync()
    => await JsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
```

> Security note: `localStorage` is **not** isolated from JavaScript on the page. If the host loads untrusted scripts the tokens are exposed. Mitigation: enforce CSP, avoid `innerHTML` with user content, prefer first-party domains for the auth cookie if XSS is a concern.

### `WasmAppConfigService : IAppConfigService`

```csharp
public class WasmAppConfigService(string baseAddress) : IAppConfigService
{
    private readonly string BaseAddress = baseAddress.TrimEnd('/');

    public Task<string?> GetServerUrlAsync() => Task.FromResult<string?>(BaseAddress);
    public Task SaveServerUrlAsync(string url) => Task.CompletedTask;  // no-op
    public Task<bool> IsConfiguredAsync() => Task.FromResult(true);
}
```

`SaveServerUrlAsync` is intentionally a no-op — a WASM bundle can't change its serving origin.

### `AddBlazorBaseUserWasm(hostBaseAddress, persistTokensInBrowserStorage)`

```csharp
public static IServiceCollection AddBlazorBaseUserWasm(
    this IServiceCollection services,
    string hostBaseAddress,
    bool persistTokensInBrowserStorage = false)
{
    if (persistTokensInBrowserStorage)
        services.AddScoped<ITokenStorage, BrowserTokenStorage>();
    else
        services.AddScoped<ITokenStorage, InMemoryTokenStorage>();

    services.AddScoped<IAppConfigService>(_ => new WasmAppConfigService(hostBaseAddress));
    return services;
}
```

---

## Code Examples

### Complete `Program.cs` for a WASM host

```csharp
using BlazorBase.Components.Services;
using BlazorBase.CRUD.Extensions;
using BlazorBase.User.Services;
using BlazorBase.User.Wasm;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBase = new Uri(builder.HostEnvironment.BaseAddress);

builder.Services.AddBlazorBaseFluentUI();
builder.Services.AddLocalization();

builder.Services.AddBlazorBaseUserClient();
builder.Services.AddBlazorBaseUserWasm(builder.HostEnvironment.BaseAddress);

builder.Services.AddHttpClient<IAuthService, AuthService>(c => c.BaseAddress = apiBase);
builder.Services.AddHttpClient<IUserService, UserService>(c => c.BaseAddress = apiBase)
    .AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddBlazorBaseCrud();
builder.Services.AddBlazorBaseCrudComponents();
builder.Services.AddBlazorBaseCrudClient(c => c.BaseAddress = new Uri(apiBase, "api/base/"))
    .AddHttpMessageHandler<AuthTokenHandler>();

await builder.Build().RunAsync();
```

### Inspecting tokens in the browser

```js
// DevTools console
localStorage.getItem("blazorbase_auth_tokens")
// → '{"accessToken":"eyJ…","refreshToken":"…","expiresAt":"2026-05-14T12:34:56Z"}'
```

### Clearing tokens on demand

```csharp
public partial class DangerZone(ITokenStorage storage,
                                BlazorBaseUserAuthStateProvider authState,
                                NavigationManager nav)
{
    #region Injects
    private readonly ITokenStorage Storage = storage;
    private readonly BlazorBaseUserAuthStateProvider AuthState = authState;
    private readonly NavigationManager Nav = nav;
    #endregion

    private async Task ForceLogoutAsync()
    {
        await Storage.ClearTokensAsync();
        await AuthState.MarkUserAsLoggedOut();
        Nav.NavigateTo("/login", forceLoad: true);
    }
}
```

### Using a different origin for the API

If the API is hosted on a different origin than the WASM bundle (e.g. `https://api.example.com` while the bundle is served from `https://app.example.com`), pass that URL to `AddBlazorBaseUserWasm` and to `AddHttpClient`. The server must enable CORS for the WASM origin.

```csharp
var apiBase = new Uri("https://api.example.com/");

builder.Services.AddBlazorBaseUserWasm(apiBase.ToString());
```

### Split-origin deployment with the cookie transport

A deployment that puts the WASM bundle at `https://app.example.com` and the API at
`https://api.example.com` — two subdomains of one registrable domain — is "same-site" but not
"same-origin". `AddBlazorBaseUserWasmCookieAuth` still works there, because the Identity application
cookie is scoped to the registrable domain rather than the exact origin, but the server-side CSRF
check defaults to rejecting it: `Sec-Fetch-Site` reads `same-site` rather than `same-origin`, and
`AuthCookieOptions.AllowSameSiteRequests` defaults to `false` for exactly that reason — a hostile or
merely compromised sibling subdomain is "same-site" too. Set it explicitly once the split-origin
deployment is the one actually in use:

```json
{
  "AuthCookies": {
    "AllowSameSiteRequests": true
  }
}
```

Doing so relies on the antiforgery token check alone to stop a cross-site request, since
`Sec-Fetch-Site` no longer distinguishes the split-origin deployment from an actual cross-site one.
