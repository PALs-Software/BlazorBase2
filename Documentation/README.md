# BlazorBase Documentation

This folder contains the documentation for all reusable BlazorBase libraries shipped inside `Libs/`. Each library is a stand-alone Razor Class Library (or .NET library) that can be referenced individually by client and server projects.

---

## Library Index

| Library | Type | Documentation |
|---|---|---|
| **BlazorBase.Localization** | .NET library (net10.0) | [BlazorBase.Localization.md](BlazorBase.Localization.md) |
| **BlazorBase.Components** | Razor Class Library (net10.0) | [BlazorBase.Components.md](BlazorBase.Components.md) |
| **BlazorBase.CRUD** | Razor Class Library (net10.0) | [BlazorBase.CRUD.md](BlazorBase.CRUD.md) |
| **BlazorBase.CRUD.Generators** | Roslyn Source Generator (netstandard2.0) | [BlazorBase.CRUD.Generators.md](BlazorBase.CRUD.Generators.md) |
| **BlazorBase.Chart** | Razor Class Library (net10.0) | [BlazorBase.Chart.md](BlazorBase.Chart.md) |
| **BlazorBase.DataProtection** | .NET library (net10.0, browser-safe) | [BlazorBase.DataProtection.md](BlazorBase.DataProtection.md) |
| **BlazorBase.Mailing** | .NET library (net10.0) | [BlazorBase.Mailing.md](BlazorBase.Mailing.md) |
| **BlazorBase.Files** | Razor Class Library (net10.0, browser-safe) | [BlazorBase.Files.md](BlazorBase.Files.md) |
| **BlazorBase.Files.Server** | ASP.NET Core library (net10.0) | [BlazorBase.Files.Server.md](BlazorBase.Files.Server.md) |
| **BlazorBase.User** | Razor Class Library (net10.0) | [BlazorBase.User.md](BlazorBase.User.md) |
| **BlazorBase.User.Server** | ASP.NET Core library (net10.0) | [BlazorBase.User.Server.md](BlazorBase.User.Server.md) |
| **BlazorBase.User.Wasm** | WebAssembly library (net10.0) | [BlazorBase.User.Wasm.md](BlazorBase.User.Wasm.md) |
| **BlazorBase.User.Maui** | MAUI library (net10.0-android/ios/maccatalyst/windows) | [BlazorBase.User.Maui.md](BlazorBase.User.Maui.md) |

---

## High-Level Architecture

All BlazorBase libraries follow the same architectural principle: a shared UI/abstraction layer that targets multiple hosts (Server, WebAssembly, MAUI) through clean DI seams.

```
                     ┌──────────────────────────────────────────────┐
                     │   Shared UI Layer (Razor Class Libraries)    │
                     │  BlazorBase.CRUD   ·  BlazorBase.User        │
                     │  BlazorBase.Chart                            │
                     └────────────┬─────────────────────────────────┘
                                  │ interfaces (IBaseDataProvider,
                                  │ ITokenStorage, IAppConfigService, …)
              ┌───────────────────┼────────────────────┐
              ▼                   ▼                    ▼
   ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
   │ Server Host      │  │ WebAssembly Host │  │ MAUI Host        │
   │ (ASP.NET Core)   │  │ (Browser)        │  │ (Native App)     │
   │                  │  │                  │  │                  │
   │ BlazorBase.User. │  │ BlazorBase.User. │  │ BlazorBase.User. │
   │   Server         │  │   Wasm           │  │   Maui           │
   │                  │  │                  │  │                  │
   │ EF Core, JWT,    │  │ localStorage,    │  │ SecureStorage,   │
   │ Identity         │  │ HttpClient       │  │ HttpClient       │
   └──────────────────┘  └──────────────────┘  └──────────────────┘
                                  │
                                  ▼
                     ┌────────────────────────────────────────────┐
                     │  REST API (Server only)                    │
                     │  /api/auth/*                               │
                     │  /api/user/*                               │
                     │  /api/base/<entity>/*                      │
                     │  /api/files        (POST upload)           │
                     │  /api/files/{id}/token (GET — issue token) │
                     │  /api/files/{id}   (GET — download, range) │
                     │  /api/files/{id}/thumbnail (GET)           │
                     │  /api/files/{id}   (DELETE)                │
                     └────────────────────────────────────────────┘
```

### Project Dependencies

```
BlazorBase.Localization    (standalone — no Blazor, no FluentUI)

BlazorBase.Components      (standalone RCL)

BlazorBase.Chart           (standalone)

BlazorBase.Mailing         (standalone)

BlazorBase.DataProtection  (standalone, browser-safe)

BlazorBase.CRUD
  ├─ BlazorBase.Components
  ├─ BlazorBase.Localization
  ├─ BlazorBase.DataProtection
  └─ BlazorBase.CRUD.Generators  (consumed as analyzer)

BlazorBase.Files           (browser-safe RCL)
  └─ BlazorBase.CRUD

BlazorBase.Files.Server    (ASP.NET Core library)
  ├─ BlazorBase.Files
  └─ BlazorBase.CRUD

BlazorBase.User
  └─ BlazorBase.CRUD

BlazorBase.User.Server
  ├─ BlazorBase.User
  └─ BlazorBase.CRUD

BlazorBase.User.Wasm
  └─ BlazorBase.User

BlazorBase.User.Maui
  └─ BlazorBase.User
```

---

## Quick Start by Scenario

### Scenario 1 — Pure Blazor Server App

```
References:
  Server  → BlazorBase.CRUD, BlazorBase.User, BlazorBase.User.Server
```

### Scenario 2 — Blazor WebAssembly + ASP.NET Core Backend

```
References:
  Server  → BlazorBase.CRUD, BlazorBase.User, BlazorBase.User.Server
  Client  → BlazorBase.CRUD, BlazorBase.User, BlazorBase.User.Wasm
  Shared  → BlazorBase.CRUD  (for DTOs / models)
```

### Scenario 3 — .NET MAUI + ASP.NET Core Backend

```
References:
  Server  → BlazorBase.CRUD, BlazorBase.User, BlazorBase.User.Server
  MAUI    → BlazorBase.CRUD, BlazorBase.User, BlazorBase.User.Maui
  Shared  → BlazorBase.CRUD
```

### Scenario 4 — Charts Only

```
References:
  Any UI host → BlazorBase.Chart
```

---

## Design Principles

1. **One shared UI library, many hosts** — UI components live in `BlazorBase.*` Razor libraries. Hosts only register platform-specific implementations.
2. **Interface-driven** — `IBaseDataProvider<T>`, `ITokenStorage`, `IAppConfigService`, `IAuditUserProvider` all decouple UI from infrastructure.
3. **Assembly scanning** — `[BaseCrud("route")]` and `[BaseEntity]` enable zero-config registration of entities, providers, endpoints and DTOs.
4. **JSON-friendly REST API** — Server endpoints (CRUD + Auth + User) are pure JSON. Clients consume them via typed `HttpClient`.
5. **Same UI, two data paths** — Server hosts hit EF Core directly via `DbContextBaseDataProvider`; WASM and MAUI hit the REST API via `HttpBaseDataProvider`. The components don't know the difference.
6. **Convention over configuration** — Audit fields, navigation properties, concurrency stamps, field labels and seeded roles all work out of the box, but every convention can be overridden.

---

## Where Things Live

| Concern | Library |
|---|---|
| List + Form components, fluent builders, custom actions | BlazorBase.CRUD |
| Lifecycle interceptors, validators, audit fields | BlazorBase.CRUD |
| Optional DTO/mapping generation | BlazorBase.CRUD.Generators |
| App shell, navigation, theming, confirmation seam, query-string reader | BlazorBase.Components |
| Localizer chaining and the empty fallback | BlazorBase.Localization |
| Chart.js wrapper (Bar/Line/Pie) | BlazorBase.Chart |
| Login / Setup / User management UI | BlazorBase.User |
| JWT auth state, token refresh handler, language service | BlazorBase.User |
| Identity, JWT issuance, Auth/User controllers | BlazorBase.User.Server |
| `localStorage` tokens, same-origin config | BlazorBase.User.Wasm |
| `SecureStorage` tokens, configurable server URL | BlazorBase.User.Maui |

---

## Conventions Used in This Documentation

- All code samples use **file-scoped namespaces** and **primary constructors with `private readonly` field assignments** (as required by the project's style guide).
- Routes shown as `api/base/<entity>` follow the default `BaseEndpointMapper` prefix; the prefix is configurable per call.
- `TUser` is the host's concrete user type (e.g. `AppUser`) that inherits from `BaseUser`.
- `TContext` is the host's concrete `DbContext` that inherits from `BaseUserDbContext<TUser>`.
