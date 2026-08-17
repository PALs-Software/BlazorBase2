# BlazorBase.User.Wasm

The Blazor WebAssembly platform implementation of the host-specific seams defined in [`BlazorBase.User`](BlazorBase.User.md). It provides:

- `BrowserTokenStorage` → `ITokenStorage` backed by `localStorage`
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
│  │  BrowserTokenStorage     localStorage                │  │
│  │  WasmAppConfigService    host.BaseAddress            │  │
│  │                                                      │  │
│  │  AddBlazorBaseUserWasm(hostBaseAddress)              │  │
│  └──────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────┘
```

**Storage key:** `blazorbase_auth_tokens` (in `localStorage`).

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

`builder.HostEnvironment.BaseAddress` is the URL the WASM app was served from. `AddBlazorBaseUserWasm` registers `ITokenStorage`, `IAppConfigService` and `IFormFactor` as **scoped** services.

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
> `AddBlazorBaseCrudComponents()` registers the interactive UI services the CRUD components depend on (`IConfirmationService`) and must be called after `AddFluentUIComponents()`.

---

## Services

### `BrowserTokenStorage : ITokenStorage`

Stores the `LoginResponse` (access token, refresh token, `ExpiresAt`) as JSON in `localStorage` under `blazorbase_auth_tokens`.

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

### `AddBlazorBaseUserWasm(hostBaseAddress)`

```csharp
public static IServiceCollection AddBlazorBaseUserWasm(this IServiceCollection services, string hostBaseAddress)
{
    services.AddScoped<ITokenStorage, BrowserTokenStorage>();
    services.AddScoped<IAppConfigService>(_ => new WasmAppConfigService(hostBaseAddress));
    return services;
}
```

---

## Code Examples

### Complete `Program.cs` for a WASM host

```csharp
using BlazorBase.CRUD.Extensions;
using BlazorBase.User.Services;
using BlazorBase.User.Wasm;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBase = new Uri(builder.HostEnvironment.BaseAddress);

builder.Services.AddFluentUIComponents();
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

builder.Services.AddHttpClient<IAuthService, AuthService>(c => c.BaseAddress = apiBase);
```
