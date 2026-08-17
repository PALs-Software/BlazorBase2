# BlazorBase.User.Maui

The .NET MAUI platform implementation of the host-specific seams defined in [`BlazorBase.User`](BlazorBase.User.md):

- `SecureTokenStorage` → `ITokenStorage` backed by MAUI's `SecureStorage` (Keychain on iOS/macOS, Keystore/EncryptedSharedPreferences on Android, DPAPI on Windows).
- `MauiAppConfigService` → `IAppConfigService` that lets the user enter a server URL on first run and stores it in `SecureStorage`.

> Target frameworks: `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`, `net10.0-windows10.0.19041.0` · References `BlazorBase.User` and `Microsoft.Maui.Controls`.

---

## Table of Contents

- [Architecture & Overview](#architecture--overview)
- [Getting Started](#getting-started)
- [Services](#services)
- [First-Run Server Setup Flow](#first-run-server-setup-flow)
- [Code Examples](#code-examples)

---

## Architecture & Overview

```
┌────────────────────────────────────────────────────────────┐
│  BlazorBase.User (shared)                                  │
│   ITokenStorage     ←┐                                     │
│   IAppConfigService ←┤  abstractions                       │
│                      │                                     │
│  ┌───────────────────┴──────────────────────────────────┐  │
│  │ BlazorBase.User.Maui                                 │  │
│  │  SecureTokenStorage      SecureStorage.Default       │  │
│  │  MauiAppConfigService    SecureStorage.Default       │  │
│  │                                                      │  │
│  │  AddBlazorBaseUserMaui()                             │  │
│  │   - registers both as SINGLETONS                     │  │
│  └──────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────┘
```

**Storage keys:**
- Tokens: `blazorbase_auth_tokens`
- Server URL: `blazorbase_server_url`

Unlike WASM, the MAUI app does not know the API URL at compile time — the user must enter it on first run via the `/setup-server` page provided by `BlazorBase.User`.

---

## Getting Started

### 1. Project reference

```xml
<ProjectReference Include="..\Libs\BlazorBase.User.Maui\BlazorBase.User.Maui.csproj" />
```

### 2. Register services

```csharp
// MauiProgram.cs

builder.Services.AddBlazorBaseUserClient();
builder.Services.AddBlazorBaseUserMaui();
```

`AddBlazorBaseUserMaui` registers `ITokenStorage`, `IAppConfigService` and `IFormFactor` as **singletons** — consistent with MAUI's long-lived app lifetime.

### 3. Wire HTTP clients dynamically based on the configured URL

> **Do not put `AuthTokenHandler` on the `IAuthService` client.** The handler injects
> `IAuthService` itself, so that combination closes a dependency cycle and the first resolve
> throws. The auth endpoints need no bearer token.


Because the API base address is unknown until `IAppConfigService.GetServerUrlAsync()` returns, configure the HTTP client with a runtime callback:

```csharp
builder.Services.AddHttpClient<IAuthService, AuthService>((sp, client) =>
{
    var config = sp.GetRequiredService<IAppConfigService>();
    var url = config.GetServerUrlAsync().GetAwaiter().GetResult();
    if (!string.IsNullOrEmpty(url))
        client.BaseAddress = new Uri(url + "/");
});

builder.Services.AddHttpClient<IUserService, UserService>((sp, client) =>
{
    var config = sp.GetRequiredService<IAppConfigService>();
    var url = config.GetServerUrlAsync().GetAwaiter().GetResult();
    if (!string.IsNullOrEmpty(url))
        client.BaseAddress = new Uri(url + "/");
})
.AddHttpMessageHandler<AuthTokenHandler>();
```

> The blocking `GetAwaiter().GetResult()` is acceptable here because `MauiAppConfigService` reads from `SecureStorage` on the UI thread which already supports synchronous waits in the typed-client factory. For finer control, wrap the resolved URL behind a service so each request reads the latest value asynchronously.

### 4. Optional — BlazorBase.CRUD HTTP client

```csharp
builder.Services.AddBlazorBaseCrud();

// Interactive UI services the CRUD components depend on (IConfirmationService) —
// call after AddFluentUIComponents()
builder.Services.AddBlazorBaseCrudComponents();

builder.Services.AddBlazorBaseCrudClient((sp, c) =>
{
    var config = sp.GetRequiredService<IAppConfigService>();
    var url = config.GetServerUrlAsync().GetAwaiter().GetResult();
    if (!string.IsNullOrEmpty(url))
        c.BaseAddress = new Uri(url.TrimEnd('/') + "/api/base/");
})
.AddHttpMessageHandler<AuthTokenHandler>();
```

### 5. Enable HTTPS cleartext on Android (debug only)

If you're testing against a local server with a self-signed cert, configure the Android `network_security_config.xml` to trust user CAs. **Never ship this in release builds.**

---

## Services

### `SecureTokenStorage : ITokenStorage`

Persists tokens (`LoginResponse`) as JSON in MAUI's platform secure storage. The exact backing store depends on the OS:

| Platform | Backing Store |
|---|---|
| Android | Android Keystore + `EncryptedSharedPreferences` |
| iOS / macCatalyst | Keychain |
| Windows | DPAPI |

```csharp
public async Task<LoginResponse?> GetTokensAsync()
{
    var json = await SecureStorage.Default.GetAsync(StorageKey);
    return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<LoginResponse>(json);
}

public async Task SaveTokensAsync(LoginResponse tokens)
    => await SecureStorage.Default.SetAsync(StorageKey, JsonSerializer.Serialize(tokens));

public Task ClearTokensAsync()
{
    SecureStorage.Default.Remove(StorageKey);
    return Task.CompletedTask;
}
```

> `SecureStorage.Remove` is synchronous, hence `Task.CompletedTask`.

### `MauiAppConfigService : IAppConfigService`

```csharp
public async Task<string?> GetServerUrlAsync()      => await SecureStorage.Default.GetAsync(ServerUrlKey);
public async Task         SaveServerUrlAsync(string url)
    => await SecureStorage.Default.SetAsync(ServerUrlKey, url.TrimEnd('/'));
public async Task<bool>   IsConfiguredAsync()
    => !string.IsNullOrEmpty(await GetServerUrlAsync());
```

Trailing slashes are trimmed before storage. `IsConfiguredAsync()` is used by the host to decide whether to show `/setup-server`.

### `AddBlazorBaseUserMaui()`

```csharp
public static IServiceCollection AddBlazorBaseUserMaui(this IServiceCollection services)
{
    services.AddSingleton<ITokenStorage, SecureTokenStorage>();
    services.AddSingleton<IAppConfigService, MauiAppConfigService>();
    services.AddSingleton<IFormFactor, MauiFormFactor>();
    return services;
}
```

---

## First-Run Server Setup Flow

```
App start
  │
  ▼
MainLayout.OnInitializedAsync
  │
  ▼
appConfig.IsConfiguredAsync()
  │
  ├─ false → Navigate to /setup-server  (SetupServer.razor)
  │             │
  │             ▼
  │           User enters URL → appConfig.SaveServerUrlAsync(url)
  │             │
  │             ▼
  │           Reload HttpClient base addresses (rebuild DI scope
  │           or use a wrapper that reads URL per request)
  │
  └─ true  → continue (authStateProvider reads tokens, routes resolve)
```

`SetupServer.razor` is included by `BlazorBase.User` at `/setup-server` and is `[AllowAnonymous]`.

---

## Code Examples

### `MauiProgram.cs`

```csharp
using BlazorBase.CRUD.Extensions;
using BlazorBase.User.Maui;
using BlazorBase.User.Services;
using Microsoft.Extensions.Logging;
using Microsoft.FluentUI.AspNetCore.Components;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        builder.Services.AddFluentUIComponents();
        builder.Services.AddLocalization();

        builder.Services.AddBlazorBaseUserClient();
        builder.Services.AddBlazorBaseUserMaui();

        builder.Services.AddHttpClient<IAuthService, AuthService>((sp, c) => ConfigureHttp(sp, c));
        builder.Services.AddHttpClient<IUserService, UserService>((sp, c) => ConfigureHttp(sp, c))
            .AddHttpMessageHandler<AuthTokenHandler>();

        builder.Services.AddBlazorBaseCrud();
        builder.Services.AddBlazorBaseCrudComponents();
        builder.Services.AddBlazorBaseCrudClient((sp, c) =>
        {
            var url = sp.GetRequiredService<IAppConfigService>().GetServerUrlAsync().GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(url))
                c.BaseAddress = new Uri(url.TrimEnd('/') + "/api/base/");
        }).AddHttpMessageHandler<AuthTokenHandler>();

        return builder.Build();
    }

    private static void ConfigureHttp(IServiceProvider sp, HttpClient client)
    {
        var url = sp.GetRequiredService<IAppConfigService>().GetServerUrlAsync().GetAwaiter().GetResult();
        if (!string.IsNullOrEmpty(url))
            client.BaseAddress = new Uri(url.TrimEnd('/') + "/");
    }
}
```

### Redirecting to `/setup-server` on first run

```csharp
public partial class MainLayout(IAppConfigService config, NavigationManager nav) : LayoutComponentBase
{
    #region Injects
    private readonly IAppConfigService Config = config;
    private readonly NavigationManager Nav = nav;
    #endregion

    protected override async Task OnInitializedAsync()
    {
        if (!await Config.IsConfiguredAsync())
            Nav.NavigateTo("/setup-server");
    }
}
```

### Resetting the configured server URL

```csharp
public partial class ServerSettings(IAppConfigService config,
                                    ITokenStorage tokens,
                                    BlazorBaseUserAuthStateProvider authState,
                                    NavigationManager nav)
{
    #region Injects
    private readonly IAppConfigService Config = config;
    private readonly ITokenStorage Tokens = tokens;
    private readonly BlazorBaseUserAuthStateProvider AuthState = authState;
    private readonly NavigationManager Nav = nav;
    #endregion

    private async Task ChangeServerAsync()
    {
        await Tokens.ClearTokensAsync();
        await AuthState.MarkUserAsLoggedOut();
        await Config.SaveServerUrlAsync(string.Empty);
        Nav.NavigateTo("/setup-server", forceLoad: true);
    }
}
```

### Inspecting MAUI secure storage (debugging)

In a debugger, you can read the stored values via `SecureStorage.Default`:
```csharp
var tokens = await SecureStorage.Default.GetAsync("blazorbase_auth_tokens");
var url    = await SecureStorage.Default.GetAsync("blazorbase_server_url");
```

Or via the platform tools (Android: `adb shell run-as`, iOS: Keychain Access).
