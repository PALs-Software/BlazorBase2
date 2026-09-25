# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repository is

**BlazorBase** — a set of reusable Blazor base libraries (CRUD, User/Identity, Charting, Mailing, Speech) shared across applications. It is a **standalone git repository** consumed by host apps as a **git submodule**, with the contained `.csproj` files referenced directly via `<ProjectReference>`. In this workspace it sits at `Libs/` inside the CineNote app, which treats these libraries as **editable, first-party code** — fix bugs and add features here directly rather than working around them in the host (no `new`-method-hiding, no parallel routes, no wrapper re-implementations). Only **generic, reusable** logic belongs here; app-specific concerns (wording, branding, role names, business rules) stay in the consuming app.

The solution is [BlazorBase.slnx](BlazorBase.slnx). Target framework is **.NET 10** for everything except the source generator (`netstandard2.0`, required for Roslyn) and the MAUI library (multi-TFM android/ios/maccatalyst/windows).

## Authoritative documentation

[Documentation/](Documentation/) holds a detailed, per-library `.md` spec for each project ([README.md](Documentation/README.md) is the index). These are the **source of truth** for the public API surface, DI registration, REST contracts and auth flow — consult them before changing behavior, and **keep them in sync** when you change a public API. The big per-library README ([BlazorBase.CRUD/README.md](BlazorBase.CRUD/README.md)) is the deepest reference for the CRUD layer.

## Build, run, benchmark

| Task | Command |
|---|---|
| Build everything | `dotnet build BlazorBase.slnx` (needs MAUI workloads because of `BlazorBase.User.Maui`) |
| Build a single library (no MAUI workloads needed) | `dotnet build BlazorBase.CRUD/BlazorBase.CRUD.csproj` |
| Run all tests | `dotnet test BlazorBase.slnx` (same MAUI caveat; the ten test projects still run if the MAUI target fails to build) |
| Install MAUI workloads (first-time) | `dotnet workload install maui-android maui-ios maui-maccatalyst` |
| Run all benchmarks | `dotnet run -c Release --project BlazorBase.CRUD.Benchmarks` |
| Filter benchmarks | `dotnet run -c Release --project BlazorBase.CRUD.Benchmarks -- --filter "*Read*"` |
| List benchmarks without running | `dotnet run -c Release --project BlazorBase.CRUD.Benchmarks -- --list flat` |

- **Package versions are managed centrally.** [Directory.Packages.props](Directory.Packages.props) declares every version once; project files carry a bare `<PackageReference Include="…" />` with no `Version`. Adding a package means one `PackageVersion` entry there plus the bare reference in the project that needs it — a `Version` in a `.csproj` is an error (NU1008), which is the point: the same package sat at four versions across the solution before this.
- **Ten xUnit test projects** cover the libraries (`*.Test`), using bUnit for the component ones. The BenchmarkDotNet suite in `BlazorBase.CRUD.Benchmarks` is separate and deliberately manual: it **only runs in Release** (BenchmarkDotNet refuses Debug), takes minutes, and is intentionally not wrapped in xUnit. See [BlazorBase.CRUD.Benchmarks/README.md](BlazorBase.CRUD.Benchmarks/README.md) for how to add a scenario and the Azure DevOps pipeline.
- **This repo owns no EF migrations.** Identity/`RefreshToken` schema comes from `BaseUserDbContext<TUser>`, but migrations are generated and applied by the **host** app against its concrete `DbContext`. The benchmark uses a throw-away SQLite `EnsureCreated()` database, so no migrations are needed there.

## Architecture: one shared UI layer, many hosts

Every library follows the same principle — UI/abstraction lives in `net10.0` Razor Class Libraries; host-specific behavior is injected through clean DI seams. A host registers platform implementations and the same components work unchanged.

```
BlazorBase.Localization     (standalone — localizer composition, no Blazor, no FluentUI)
BlazorBase.Components       (standalone — layout, navigation, theming, editors, routing helpers)
BlazorBase.Chart            (standalone — Chart.js interop)
BlazorBase.Mailing          (standalone — SMTP + Razor-rendered email)
BlazorBase.DataProtection   (standalone — browser-safe encryption)
BlazorBase.Speech           (standalone — push-to-talk, narrator, Markdown-to-speech)
BlazorBase.Speech.Server    → BlazorBase.Speech   (ASP.NET Core: authenticated proxy to a speech service)
BlazorBase.CRUD             → BlazorBase.Components, BlazorBase.Localization, BlazorBase.DataProtection
  └─ BlazorBase.CRUD.Generators   (consumed as an analyzer, optional)
BlazorBase.User             → BlazorBase.CRUD
BlazorBase.User.Server      → BlazorBase.User, BlazorBase.CRUD   (ASP.NET Core: Identity + JWT)
BlazorBase.User.Wasm        → BlazorBase.User   (localStorage token storage)
BlazorBase.User.Maui        → BlazorBase.User   (SecureStorage token storage)
```

### CRUD: the `IBaseDataProvider<TModel>` seam (the central idea)

All CRUD UI components (`BaseList`, `BaseCard`, `BaseListPart`, `BaseLookupDialog`) talk **only** to `IBaseDataProvider<TModel>`. Two implementations are swapped by DI depending on render mode — the components never know which:

- **Server** → `DbContextBaseDataProvider<T>` (direct EF Core).
- **WASM / MAUI** → `HttpBaseDataProvider<T>` (REST over `HttpClient`, 6 endpoints: `POST query`, `GET {id}`, `GET count`, `POST`, `PATCH {id}`, `DELETE {id}`).

Consequences that drive the design and are easy to get wrong:

- **The server `DbContext` is registered as a *factory* (`IDbContextFactory`), not scoped.** Each provider operation creates and disposes its own context — thread-safe and compatible with long-lived Blazor Server circuits. `BaseSaveChangesInterceptor` is attached to the factory and populates `AuditModel` fields (`CreatedOn/By`, `ModifiedOn/By`) on every `SaveChanges`.
- **Dynamic field selection** flows end-to-end: `BaseList` builds a `Select` list from its column property names + `Id`; the server builds an `Expression.MemberInit` projection for exactly those fields (cached per field-combination in a `ConcurrentDictionary`) so only requested columns leave the database.
- **Dirty tracking + concurrency**: `BaseCard` snapshots the model on load and `PatchAsync`-es only changed fields; `AuditModel.ModifiedOn` (`[ConcurrencyCheck]`) is the optimistic-concurrency stamp, surfaced as `409 Conflict`.
- **The fluent query builder** (`provider.Query().Where(...).OrderBy(...).Select(...)`) decomposes `Where` lambdas into a `FilterDescriptor` tree on the client and ships it to the server — so the same expression works over both EF and HTTP providers.

### CRUD: zero-config registration via assembly scanning

- `[BaseCrud("route")]` on an entity marks it for discovery. `AddBlazorBaseCrudServer<TContext>()` registers a `DbContextBaseDataProvider<T>`, `AddBlazorBaseCrudClient(...)` registers an `HttpBaseDataProvider<T>`, and `MapBlazorBaseCrudEndpoints()` maps the REST endpoints — all by scanning for this attribute.
- `[CrudAccess(roles, "RIMD")]` (class- and property-level) drives R/I/M/D security, **enforced both server-side** (endpoint guards + response field-stripping) **and client-side** (component visibility / read-only). Hierarchy is **Class > Property > View**; each level can only further restrict, and multiple matching attributes on a level union their rights.
- **Entity = Model**: entities are the models the components bind to, so DTOs are not needed for standard CRUD. `BlazorBase.CRUD.Generators` (`[BaseEntity]`) is an **optional** incremental source generator for the rare cases that need a distinct DTO shape; reference it with `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`.

### User/auth: generic over the host's user type

The auth pipeline is generic on `TUser : BaseUser` and `TContext : BaseUserDbContext<TUser>`, letting a host add custom user properties without forking the pipeline. `AuthControllerBase<TUser>` and `UserControllerBase<TUser>` are **abstract** — the host must derive thin concrete `[ApiController]` classes so ASP.NET Core discovers them. JWT access tokens are short-lived; refresh tokens are persisted and **rotated** on every refresh (old token revoked). On the client, `AuthTokenHandler` (a `DelegatingHandler`) attaches the bearer, proactively refreshes within 1 minute of expiry, and retries once on `401`. `ITokenStorage` / `IAppConfigService` are the platform seams (Wasm = `localStorage` + same-origin; Maui = `SecureStorage` + user-entered URL). See [Documentation/BlazorBase.User.Server.md](Documentation/BlazorBase.User.Server.md) and [Documentation/BlazorBase.User.md](Documentation/BlazorBase.User.md).

### BlazorBase.Components: the shared UI layer

Layout, navigation, theming and the standalone editors live here, deliberately below CRUD and User in
the graph. They know nothing about entities or identity, so neither project is the right owner: an app
that only needs a data grid should not have to reference the auth stack to get an app shell, and a
`RichTextEditor` is not a CRUD concept.

- `Layout/` — `BaseLayout` (header, navigation slot, body) plus `Navigation/`. One `NavigationItem`
  tree drives both `BaseSideNavigation` (desktop rail) and `BaseBottomNavigation` (mobile tab bar with
  an overflow sheet), filtered once by `NavigationVisibility` so role visibility cannot diverge between
  form factors.
- `Editors/`, `Diff/`, `Files/`, `Html/` — `RichTextEditor`, `DiffViewer`, `FileTree`, `SanitizedHtml`,
  `MarkdownView` (Markdig, safe without a sanitizer — see the Components documentation before loosening it).
  CRUD consumes them; none of them consumes CRUD.
- `Services/` — `IThemeService`, `ILanguageService`, `IFormFactor`, `IConfirmationService`.
- `Routing/` — `QueryStringReader`, a dependency-free query-parameter reader.

### Speech: browser audio through an authenticated proxy

`BlazorBase.Speech` records push-to-talk audio (16 kHz mono WAV via an `AudioWorklet`) and reads text
aloud (`ISpeechNarrator`: Markdown → `SpeechTextPreparer` segments → synthesis of the next segment while
the current one plays). `BlazorBase.Speech.Server` holds the abstract `SpeechControllerBase`
(`api/speech/*`) that proxies to an **unauthenticated internal** speech service speaking OpenAI-shaped
`/v1/audio/*` routes — the browser never reaches that service. Audio crosses the JS boundary as streams,
not serialized arguments, so it also works under Blazor Server's SignalR size limit. Two traps: the
microphone is released after every press because Safari on iOS routes playback to the earpiece while a
microphone is open, and every press primes playback because browsers require a user gesture before
audio may play. See [Documentation/BlazorBase.Speech.md](Documentation/BlazorBase.Speech.md) and
[Documentation/BlazorBase.Speech.Server.md](Documentation/BlazorBase.Speech.Server.md).

### Chart & Mailing (standalone)

- **BlazorBase.Chart** — `Bar`/`Line`/`Pie` components over a Chart.js JS module loaded via `ChartInterop` (`./_content/BlazorBase.Chart/js/blazorChart.js`); strongly-typed `ChartConfig`/`ChartOptions` models.
- **BlazorBase.Mailing** — `AddBlazorBaseMailing(configuration, sectionName = "Smtp")` wires `SmtpSettings` (options pattern), a MailKit-based `IEmailSender`, and an `IEmailTemplateRenderer` that renders Razor components to HTML via the built-in `HtmlRenderer`.

## Conventions for code in this repo

Global C# / Razor style rules (file-scoped namespaces; primary constructors with `private readonly` fields wrapped in a `#region Injects` block; Razor code-behind instead of `@code`; CSS isolation instead of inline `style`; EF config via Data Annotations; no inline comments; omit braces for single-statement blocks; guard clauses with early exit) live in the user's global `~/.claude/CLAUDE.md` and are already followed throughout — match the surrounding code. Repo-specific points on top of those:

- **Keep it generic.** Anything entity-, brand- or business-specific does not belong in `BlazorBase.*` — it goes in the host app.
- **Update [Documentation/](Documentation/) alongside public-API changes**, and update the per-project `README.md` for the CRUD layer. The docs double as the API contract.
- **`IStringLocalizer` resolution** in CRUD components has a defined fallback chain (explicit value → explicit `Localizer` → model `IStringLocalizer<TModel>` → **its base-type localizers up the inheritance chain** → raw property name); name localizer fields `Localizer`, never single letters. The base-type walk (`LocalizerResolver.ResolveProperty`) lets a base entity such as `AuditModel` carry shared captions/tooltips once (`Core/AuditModel.resx`) for every derived model.
- **Model-near localization is the default for CRUD captions.** Do **not** set `Title`/`Label`/`Tooltip`/`Placeholder` in `BaseColumn`/`PropertyField` markup. Instead add a co-located `<Entity>.resx`/`<Entity>.de.resx` (next to the entity `.cs`, in the host app) keyed by property name; the column title, field label, list-part header, and filter label all resolve from it automatically. Conventions: label/title key = `<PropertyName>`; tooltip key = `<PropertyName>_Tooltip` (FluentUI `HeaderTooltip` on columns, `FluentTooltip` on fields/list-part headers — shown only when the key exists); placeholder key = `<PropertyName>_Placeholder` (use sparingly). Explicit `Title=`/`Label=` is reserved for **synthetic** columns whose caption is not the bound property (e.g. an "Actions" template column bound to `Id`).
- **Split `BaseList` and `BaseCard` into separate components (default).** When a screen pairs a `BaseList<TModel>` with a card, the card is its own component referenced via `CardType="typeof(XxxCard)"`, **not** an inline `BaseCardConfiguration`/`BaseCardBuilder` built in the list's code-behind. The card component declares the parameters the dialog passes (`Model`, `DataProvider`, `IStringLocalizer? Localizer`, `BaseCardConfiguration<TModel>? Configuration`, `EventCallback<TModel> OnAfterSave`) and forwards them to an inner `BaseCard` whose fields are `<PropertyField>` markup. A list part's per-item card is a separate sub-card too, wired via `<ListPartField ChildCardType="typeof(XxxChildCard)" />` (or the Fluent `ListPartBuilder.ChildCardType<T>()`); the sub-card takes the defer-save contract (`Model`, `Localizer`, `bool DeferSave`, `EventCallback<Dictionary<string, object?>> OnDeferredSave`, `EventCallback OnCancelled`). The Fluent `BaseCardBuilder` stays available for dynamic/computed configs, but markup-split components are the default for new screens. `BlazorBase.User.Pages.UserManagementView` (→ `Components/UserCard`) is the in-repo reference.
