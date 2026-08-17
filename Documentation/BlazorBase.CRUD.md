# BlazorBase.CRUD

A reusable Razor Class Library for type-safe CRUD operations with **Fluent UI Blazor**. Provides ready-made list/card components, an abstracted data layer (`IBaseDataProvider<T>`), minimal-API CRUD endpoints, role-based field security, optimistic concurrency, audit fields and a LINQ-style fluent query builder — all generic and extensible.

> Target framework: `net10.0` · UI components live here once and are consumed by Server, WebAssembly and MAUI hosts.

> A long-form, example-rich reference is also kept inside the project itself at [`Libs/BlazorBase.CRUD/README.md`](../BlazorBase.CRUD/README.md). This document focuses on architecture, lifecycle and the public API surface, and is the canonical source for the Documentation folder.

---

## Table of Contents

- [Architecture & Overview](#architecture--overview)
- [Getting Started](#getting-started)
- [Defining Entities](#defining-entities)
- [Component Reference](#component-reference)
- [API Reference](#api-reference)
- [Code Examples](#code-examples)

---

## Architecture & Overview

```
┌────────────────────────────────────────────────────────┐
│  UI Components (Razor)                                 │
│   BaseList<TModel>     BaseCard<TModel>                │
│   BaseListPart<TModel> BaseLookupDialog<TModel>        │
│   BaseDialog<TModel>   BaseActionToolbar<TModel>       │
│   BaseColumn / PropertyField / ListPartField           │
│   BaseActionGroup / BaseAction                         │
├────────────────────────────────────────────────────────┤
│  Fluent Configuration                                  │
│   BaseListBuilder<T>   BaseCardBuilder<T>              │
│   CrudActionBuilder / CrudActionGroupBuilder           │
├────────────────────────────────────────────────────────┤
│  Data Abstraction                                      │
│   IBaseDataProvider<TModel>                            │
│   ├─ DbContextBaseDataProvider<T>  (Server / EF Core)  │
│   └─ HttpBaseDataProvider<T>       (Client / REST)     │
├────────────────────────────────────────────────────────┤
│  Query Layer                                           │
│   BaseQueryBuilder<T> ┐                                │
│   FilterExpression    ├─ → BaseQuery (Filters, Sorts,  │
│   Decomposer          │      Select, NavigationFilters)│
│   NavigationFilter    ┘                                │
├────────────────────────────────────────────────────────┤
│  Lifecycle Hooks                                       │
│   IBaseDataInterceptor<TModel>                         │
│   IBaseValidator<TModel>                               │
│   BaseSaveChangesInterceptor (EF Core, audit fields)   │
├────────────────────────────────────────────────────────┤
│  Server Endpoints (Minimal API)                        │
│   POST /api/base/<route>/query   GET /<id>             │
│   GET  /count    POST /          PATCH /<id>           │
│   DELETE /<id>                                         │
│  Auto-mapped via [BaseCrud("route")] + assembly scan   │
└────────────────────────────────────────────────────────┘
```

### Render-mode matrix

| Render mode | DataProvider | Direct DB access |
|---|---|:---:|
| Blazor Server / Interactive Server | `DbContextBaseDataProvider<T>` | yes (EF Core) |
| WebAssembly | `HttpBaseDataProvider<T>` | no — REST |
| MAUI / Hybrid | `HttpBaseDataProvider<T>` | no — REST |

UI components only ever depend on `IBaseDataProvider<TModel>`. The host decides which implementation is registered.

> ### ⚠️ Security enforcement boundary (CRUD-UI-03)
>
> The server-side `[CrudAccess]` **field/row enforcement** — the query field-access validator (a `403`
> when a filter/sort targets an unreadable field), the response field-stripping sanitizer, and the
> `UserFilter` row-scoping — lives **only in the REST endpoint path** (`BaseEndpointMapper`, used by the
> WebAssembly/MAUI `HttpBaseDataProvider`). The **Blazor-Server / Interactive-Server render mode injects
> `DbContextBaseDataProvider<T>` directly into the components**, which call EF Core without going through
> the endpoint mapper — so on that path the validator, sanitizer and row filter do **not** run, and the
> component-level `[CrudAccess]` checks (`CanAdd`/`CanEdit`/column visibility) are **UX affordances, not a
> security boundary**. A malicious or buggy server-side caller that reaches the provider directly is not
> constrained by them.
>
> **Guidance:** on Blazor-Server hosts, enforce authorization server-side (a `scopeFilter`/`UserFilter`,
> `[Authorize]` on the page/circuit, and per-entity checks) rather than relying on the component's
> `[CrudAccess]` visibility. Do not treat the interactive-server UI's field hiding as an access-control
> guarantee. (A follow-up work item tracks wiring the endpoint-path enforcement pipeline into
> `DbContextBaseDataProvider` so both render modes share one server-side boundary.)

---

## Getting Started

### 1. Project reference

```xml
<ProjectReference Include="..\Libs\BlazorBase.CRUD\BlazorBase.CRUD.csproj" />
```

### 2. Core registration (every host)

```csharp
builder.Services.AddBlazorBaseCrud();
```

Or, with an audit-user provider:

```csharp
builder.Services.AddBlazorBaseCrud<MyAuditUserProvider>();
```

### 3. Server host — DbContext + endpoints

```csharp
builder.Services.AddBaseDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));
builder.Services.AddBlazorBaseCrudServer<AppDbContext>();

app.MapBlazorBaseCrudEndpoints();
```

Notes:
- `AddBaseDbContext<T>` registers a `IDbContextFactory<T>` and injects `BaseSaveChangesInterceptor`, which fills `CreatedOn / CreatedBy / ModifiedOn / ModifiedBy` automatically.
- Each `DbContextBaseDataProvider<T>` creates and disposes its own `DbContext` per call — safe for long-lived Blazor Server circuits.
- `MapBlazorBaseCrudEndpoints()` scans for `[BaseCrud("route")]` and registers all 6 endpoints per entity.

### 4. WebAssembly / MAUI host — HTTP provider

```csharp
builder.Services.AddBlazorBaseCrud();

// Interactive UI services the CRUD components depend on (IConfirmationService) —
// call after AddFluentUIComponents()
builder.Services.AddBlazorBaseCrudComponents();

builder.Services.AddBlazorBaseCrudClient(client =>
{
    client.BaseAddress = new Uri("https://api.example.com/api/base/");
});
```

The route from `[BaseCrud("products")]` is appended to this base address by `HttpBaseDataProvider`.

### 5. Registration summary

| Call | Server | WASM | MAUI |
|---|:---:|:---:|:---:|
| `AddBlazorBaseCrud()` | ✓ | ✓ | ✓ |
| `AddBlazorBaseCrudComponents()` | — | ✓ | ✓ |
| `AddBaseDbContext<T>(…)` | ✓ | — | — |
| `AddBlazorBaseCrudServer<T>()` | ✓ | — | — |
| `AddBlazorBaseCrudClient(…)` | — | ✓ | ✓ |
| `MapBlazorBaseCrudEndpoints()` | when WASM/MAUI clients consume the API | — | — |
| `AddBaseDataInterceptor<T,I>()` | ✓ | optional | optional |
| `AddBaseValidator<T,V>()` | ✓ | ✓ | ✓ |

### 6. Layout — the Fluent UI providers

**Registration is not enough. The host layout has to carry the Fluent UI providers**, because
`BaseList<TModel>` and `BaseCard<TModel>` open their dialogs through Fluent UI's `IDialogService`
and the toolbar and property inputs place `FluentTooltip` and `FluentMenu` in markup.

Without the dialog provider, opening an item or pressing *add* throws and the component tree fails
to render:

```
ArgumentNullException: <FluentDialogProvider /> needs to be added to the main layout of your
application/site. (Parameter 'OnShowAsync')
```

`BlazorBase.User`'s `BaseLayout` already carries the full set, so a host that uses it needs nothing
further. **A host with its own layout has to add them itself:**

```razor
@* At the end of MainLayout.razor, outside the layout markup *@
<FluentToastProvider />
<FluentDialogProvider />
<FluentTooltipProvider />
<FluentMessageBarProvider />
<FluentMenuProvider />
```

The failure is easy to miss during development because it only appears on the screens that open a
dialog — a dashboard or a custom list renders perfectly without any provider.

---

## Defining Entities

```csharp
[BaseCrud("products")]
public class Product
{
    public Guid Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    [CrudAccess("*", "")]
    public string InternalNotes { get; set; } = string.Empty;
}

[BaseCrud("orders")]
public class Order : AuditModel
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public ICollection<OrderItem> Items { get; set; } = [];
}
```

### Attributes

| Attribute | Target | Purpose |
|---|---|---|
| `[BaseCrud("route")]` | class | Mark entity for assembly-scanning DI + endpoint mapping |
| `[CrudAccess(roles, "RIMD")]` | class & property | Grant R/I/M/D rights to roles. Multiple allowed (union per level). Use `[CrudAccess("*", "")]` to exclude a property entirely (replaces `[CrudIgnore]`). |
| `[BaseEntity]` | class | Trigger DTO source generator (see [BlazorBase.CRUD.Generators](BlazorBase.CRUD.Generators.md)) |

### Navigation property detection

- **Reference navigation** (`Category Category`) — auto-detected via FK convention (`CategoryId` / `CategoryID`) or `[ForeignKey]`. Rendered as `FluentSelect` (≤ 20 items) or a search/browse panel (> 20 items, configurable threshold).
- **Collection navigation** (`ICollection<OrderItem> Items`) — auto-rendered as a `BaseListPart` inside `BaseCard`.

The reference-navigation lookup (the `FluentSelect`/browse panel that lists candidate target entities)
now checks the current user's `[CrudAccess]` **Read** right on the navigation target before loading any
candidates (CRUD-UI-02). The check intersects the owner navigation property's rights with the target
class's Read (`CrudAccessResolver.EvaluateNavigationTarget`); if the user may not read the target, the
lookup is not populated (no count or values of the foreign type leak) and the field renders **read-only**,
showing the already-bound value. When no `AuthenticationStateProvider` is registered the check is skipped
(single-user/no-auth posture — this matches the framework-wide component behavior; register an
authentication state provider for `[CrudAccess]` to be enforced in the UI).

### Audit fields

`AuditModel` provides `CreatedOn / CreatedBy / ModifiedOn / ModifiedBy`. `ModifiedOn` carries `[ConcurrencyCheck]` so it doubles as the optimistic concurrency stamp.

Audit fields are fully **server-authoritative**: `BaseSaveChangesInterceptor` sets `CreatedOn`/`CreatedBy` on insert and `ModifiedOn`/`ModifiedBy` on update, and reverts any client-supplied `CreatedOn`/`CreatedBy` back to their original database values on update. `DbContextBaseDataProvider<T>.PatchAsync` never applies these fields from a PATCH payload — they cannot be set or corrected through the provider or the REST endpoint, only through the interceptor itself.

> **Overposting note (CRUD-DA-06).** Beyond the audit fields above, the create endpoint binds whatever
> the JSON body carries. A client-supplied primary-key `Id` and foreign-key/navigation scalars are
> **bindable on create** unless you explicitly lock them — an entity that must not accept a client-chosen
> key or FK on insert should mark those properties `[CrudAccess("*", "")]` (or a role-restricted
> `[CrudAccess]` without `I`) so `ValidatePropertyRights` rejects an attempt to set them. Prefer
> server-generated keys (`[DatabaseGenerated(DatabaseGeneratedOption.Identity)]`) and assign FKs from the
> authenticated context rather than trusting the create payload.

To populate `CreatedBy`/`ModifiedBy` with the current user, implement `IAuditUserProvider` and register it:

```csharp
public class HttpAuditUserProvider(IHttpContextAccessor accessor) : IAuditUserProvider
{
    private readonly IHttpContextAccessor Accessor = accessor;
    public string? GetCurrentUserId()
        => Accessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
```

```csharp
builder.Services.AddBlazorBaseCrud<HttpAuditUserProvider>();
```

---

## Component Reference

### `BaseList<TModel>`

Renders a `FluentDataGrid` with sorting, filtering, infinite scroll, row-click edit, multi-select, context menus and a configurable action toolbar.

Key parameters:

| Parameter | Type | Notes |
|---|---|---|
| `Configuration` | `BaseListConfiguration<TModel>?` | From `BaseListBuilder` (takes precedence over child components) |
| `CardConfiguration` | `BaseCardConfiguration<TModel>?` | Used by the auto-edit `BaseDialog` |
| `DataProvider` | `IBaseDataProvider<TModel>?` | Optional — resolved from DI if omitted |
| `Localizer` | `IStringLocalizer?` | Override label/title source |
| `AllowAdd` / `AllowEdit` / `AllowDelete` | `bool` | Default: `true` |
| `NavigationFilters` | `IList<NavigationFilter>?` | Filtered includes |
| `ActionGroups` | `IList<CrudActionGroup<TModel>>?` | Dynamic action groups |

UI features:
- **Click-to-edit row** + hover highlight + pointer cursor
- **Right-click context menu** (Edit / Delete / Select / custom actions). The menu acts on the row the
  pointer is over, which the list learns from cell focus — a data-grid row is a `tr` with
  `display: contents` and cannot be focused itself.
- **Empty state** — with no items the grid is not rendered at all and only `EmptyText` (default:
  the framework's localized "no items found") is shown, so FluentUI's own untranslated message never
  appears beside it.
- **Boolean columns** render the framework's localized `BoolTrue`/`BoolFalse` ("Yes"/"No", "Ja"/"Nein"),
  the same wording the filter panel offers for the property — not the raw CLR `True`/`False`.
- **Column width** — a column's `Width` (markup `Width="120px"` or fluent `.Width("120px")`) is any
  valid CSS grid track. Columns left without one share the remaining space equally but never shrink
  below what their content needs (`--base-list-column-min-width`, default `max-content`), so a narrow
  viewport scrolls the grid sideways instead of truncating every column to an ellipsis. Give a column
  holding long prose an explicit `Width`, or set the custom property to a fixed length.
- **Shift+Click multi-select** + auto-appearing selection-indicator column
- **Bulk delete** with confirmation
- **Per-column** `Sortable` / `Filterable` (default: both `true`)
- **Role-based column visibility** via `[CrudAccess]` on the entity or `.Access(roles, rights)` on the column builder

### `BaseCard<TModel>`

Detail/edit form for a single entity. Generic constraint: `where TModel : class, new()`.

| Parameter | Type | Notes |
|---|---|---|
| `Model` | `TModel` | The bound entity |
| `Configuration` | `BaseCardConfiguration<TModel>?` | From `BaseCardBuilder` |
| `MaxColumns` | `int` | Responsive CSS grid |
| `DataProvider` | `IBaseDataProvider<TModel>?` | Optional |
| `IsNew` | `bool?` | Whether the model is being created. Unset, the key is inspected instead |
| `OnAfterSave` | `EventCallback<TModel>` | |

Behavior:
- Tracks **dirty fields** by snapshotting the model on load and diffing on submit.
- Sends only changed fields via `PatchAsync`, including the concurrency stamp for `AuditModel` entities.
- **Decides create versus update from `IsNew` when given, otherwise from the key.** `BaseDialog` passes
  what `BaseList` already knows, so the dialog path is always exact — including through a custom
  `CardType`, which receives the answer as a cascading value (`CardCascadeNames.IsNew`) rather than a
  parameter, so a wrapper card needs to declare and forward nothing. Set it yourself when hosting a
  card directly and the model **assigns its key before saving** — a key-based guess then reads a new
  model as an existing one, which captions the header "edit", checks modify instead of insert rights,
  and sends the save down the update path. The guess treats `null`, the CLR default and an **empty
  string** as unset.
- The header shows the display key, falling back to an "add"/"edit" caption. Mark a human-readable
  property with `[DisplayKey]` on any entity whose key reads poorly — without it a GUID-keyed entity
  titles its card with the GUID.
- Catches **409 Conflict** and shows a friendly `FluentMessageBar`.
- Renders inputs via `BasePropertyInput` (typed automatically: string → `FluentTextField`, bool → `FluentSwitch`, enum → `FluentSelect`, DateTime → `FluentDatePicker`, navigation → select or browse).

### `BaseListPart<TModel>`

Embedded child collection inside a `BaseCard`. Same generic constraint as `BaseCard`.

### `BaseLookupDialog<TModel>`

Modal lookup dialog with search/pagination, used by the navigation "Browse…" button or invoked manually.

### `BaseActionGroup<TModel>` / `BaseAction<TModel>` / `BaseActionToolbar<TModel>`

Declarative child components mirroring the fluent action API. Both approaches can be mixed — declarative items and builder configuration are merged at runtime.

`CrudActionContext` (flags): `ListToolbar`, `ContextMenu`, `Card`, `ListPart`, `All`.

### Registrable custom property components

DI-registered, reusable controls that the generic card and list resolve automatically per property.
Generic and WASM-safe (data flows via parameters; no `DbContext`).

| Type | Role |
|---|---|
| `IBaseCustomPropertyInput` | Card edit/detail control. `bool CanHandle(CustomPropertyContext)`; params `object Model`, `PropertyInfo Property`, `object? Value`, `EventCallback<object?> ValueChanged`, `bool IsEditing`, `bool ReadOnly`, `IStringLocalizer? Localizer`. |
| `IBaseCustomPropertyDisplay` | List-cell display. `CanHandle` (`IsEditing` always `false`); params `object Model`, `PropertyInfo Property`, `object? Value`, `IStringLocalizer? Localizer`. |
| `ICardSaveParticipant` | Optional save-lifecycle contract on a custom input: `ValidateAsync()` (false blocks save), `OnBeforeSaveAsync(CardSaveContext)`, `OnAfterSaveAsync(CardSaveContext)`. |
| `CustomPropertyContext` | `record (Type ModelType, PropertyInfo Property, Type PropertyType /* nullable unwrapped */, bool IsEditing)`. `CanHandle` must be synchronous, pure, cheap. |
| `CardSaveContext` | `object Model`, `bool IsCreate`, `AddMessage(string)` / `Message`. |

Register with `AddBlazorBaseCustomInput<TComponent>()` / `AddBlazorBaseCustomDisplay<TComponent>()`
(scoped). Multiple allowed; the **first** registered component whose `CanHandle` is `true`
(registration order) wins, cached per `(model type, property, edit mode)`.

**Render precedence (card field):** per-field `EditorTemplate` / `DisplayTemplate` →
registered custom input → built-in type chain. **(list cell):** column `Template` →
registered custom display → default cell text. Purely additive: existing behavior and the per-field
template are unchanged unless a component is registered *and* `CanHandle` matches.

**Save lifecycle:** on `BaseCard` save, every collected `ICardSaveParticipant` runs
`ValidateAsync` (a `false` aborts the save), then `OnBeforeSaveAsync`, then the `IBaseDataProvider`
persist, then `OnAfterSaveAsync` — enabling generic stateful cases (e.g. reveal a generated secret
once in `OnAfterSaveAsync`).

---

## API Reference

### Attributes

| Attribute | Target | Description |
|---|---|---|
| `BaseCrudAttribute("route")` | class | Marks entity for auto-registration |
| `CrudAccessAttribute(roles, rights)` | class & property | Grants R/I/M/D rights to one or more roles. Multiple allowed; combined via union. |
| `BaseEntityAttribute` | class | Triggers DTO source generator |

### Core interfaces

```csharp
namespace BlazorBase.CRUD.Core;

public interface IBaseDataProvider<TModel>
{
    Task<BaseQueryResult<TModel>> GetListAsync(BaseQuery query, Expression<Func<TModel, bool>>? scopeFilter = null, CancellationToken ct = default);
    Task<TModel?>                 GetByIdAsync(object id, IEnumerable<string>? select = null, Expression<Func<TModel, bool>>? scopeFilter = null, CancellationToken ct = default);
    Task<int>                     GetCountAsync(Expression<Func<TModel, bool>>? scopeFilter = null, CancellationToken ct = default);
    Task<TModel>                  CreateAsync(TModel model, CancellationToken ct = default);
    Task<TModel>                  PatchAsync(object id, Dictionary<string, object?> changedFields, string? concurrencyStamp = null, CancellationToken ct = default);
    Task                          DeleteAsync(object id, CancellationToken ct = default);
}
```

`scopeFilter` is a server-only row-level-security predicate, inserted ahead of `CancellationToken` on every read method — callers that previously passed `CancellationToken` positionally must switch to `ct:`/`cancellationToken:`. It is applied by `DbContextBaseDataProvider<T>` (added as a `Where(scopeFilter)` clause), **ignored** by `HttpBaseDataProvider<T>` (clients never scope rows themselves — see [`BaseEndpointOptions.UserFilter`](#rest-endpoints-per-entity) for the server-side equivalent), and **unsupported** by the Identity-backed `UserDataProvider`, which throws `NotSupportedException` when a non-null `scopeFilter` is passed.

```csharp
public interface IAuditUserProvider
{
    string? GetCurrentUserId();
}

public abstract class AuditModel
{
    public DateTime CreatedOn  { get; set; }
    public string?  CreatedBy  { get; set; }

    [ConcurrencyCheck]
    public DateTime ModifiedOn { get; set; }
    public string?  ModifiedBy { get; set; }
}
```

### Lifecycle hooks

```csharp
public interface IBaseDataInterceptor<TModel>
{
    Task<TModel>                       OnBeforeCreateAsync(TModel model, CancellationToken ct = default);
    Task                               OnAfterCreateAsync(TModel model, CancellationToken ct = default);
    Task<Dictionary<string, object?>>  OnBeforePatchAsync(object id, Dictionary<string, object?> changedFields, CancellationToken ct = default);
    Task                               OnAfterPatchAsync(TModel model, CancellationToken ct = default);
    Task                               OnBeforeDeleteAsync(object id, TModel? model, CancellationToken ct = default);
    Task                               OnAfterDeleteAsync(object id, CancellationToken ct = default);
    Task<BaseQuery>                    OnBeforeQueryAsync(BaseQuery query, CancellationToken ct = default);
}

public interface IBaseValidator<TModel>
{
    Task<IEnumerable<ValidationResult>> ValidateAsync(TModel model, CancellationToken ct = default);
}
```

### BaseValidationException

Throw `BaseValidationException` from an `IBaseDataInterceptor<TModel>` method to signal a business-rule violation. The server endpoint catches it and returns **400 Bad Request** with a JSON body `{ error: "validation", message: "…" }`. `HttpBaseDataProvider` translates the 400 back into a `BaseValidationException`, and `BaseCard` shows the message in its error area — the same `FluentMessageBar` used for concurrency conflicts.

```csharp
public class WorkitemInterceptor : BaseDataInterceptor<Workitem>
{
    public override Task<Workitem> OnBeforeCreateAsync(
        Workitem model, CancellationToken ct = default)
    {
        if (model.AreaId.HasValue && model.Area?.ProjectId != model.ProjectId)
            throw new BaseValidationException("The selected Area does not belong to the chosen Project.");

        return Task.FromResult(model);
    }
}
```

The full path: interceptor `throw` → `BaseEndpointMapper` catches → `400 BadRequest({ error, message })` → `HttpBaseDataProvider` translates → `BaseValidationException` → `BaseCard.OnValidSubmitAsync` catches → `ErrorMessage = ex.Message` → UI `FluentMessageBar`.

### Query types

| Class | Purpose |
|---|---|
| `BaseQuery` | `Filters`, `Sorts`, `Select`, `NavigationFilters`, `Skip`, `Take` |
| `BaseQueryResult<T>` | `Items`, `TotalCount` |
| `FilterDescriptor` | `PropertyName`, `Operator`, `Value` — or group: `Logic`, `Filters` |
| `FilterOperator` | `Equals`, `NotEquals`, `Contains`, `StartsWith`, `EndsWith`, `GreaterThan`, `LessThan`, `IsNull`, `IsNotNull`, `In` |
| `FilterLogic` | `And`, `Or` |
| `SortDescriptor` | `PropertyName`, `Direction` |
| `NavigationFilter` | `NavigationName`, `Filters`, `Sorts` (filtered include) |
| `PatchModel` | `ChangedFields` + `ConcurrencyStamp` |

### FilterOperator catalog

| Operator | Applies to | Description | Example |
|---|---|---|---|
| `Equals` | all scalars | Property equals value | `p.Name == "Widget"` |
| `NotEquals` | all scalars | Property does not equal value | `p.Name != "Widget"` |
| `Contains` | string | Property contains substring | `p.Name.Contains("get")` |
| `StartsWith` | string | Property starts with prefix | `p.Name.StartsWith("W")` |
| `EndsWith` | string | Property ends with suffix | `p.Name.EndsWith("t")` |
| `GreaterThan` | numeric, date | Property is greater than value | `p.Price > 10` |
| `GreaterThanOrEqual` | numeric, date | Property is greater than or equal to value | `p.Price >= 10` |
| `LessThan` | numeric, date | Property is less than value | `p.Price < 10` |
| `LessThanOrEqual` | numeric, date | Property is less than or equal to value | `p.Price <= 10` |
| `IsNull` | nullable | Property is null | `p.Description == null` |
| `IsNotNull` | nullable | Property is not null | `p.Description != null` |
| `In` | any scalar | Property value is a member of the supplied set — translates to SQL `IN (…)`. The filter `Value` must be an `IEnumerable` of the property type; over HTTP it round-trips as a JSON array. Use `collection.Contains(e.Property)` in a `Where` lambda to decompose directly to this operator. | `ids.Contains(e.LocationId)` |

### DI extensions

| Method | Purpose |
|---|---|
| `AddBlazorBaseCrud()` | Core (host-agnostic) services |
| `AddBlazorBaseCrud<TAuditUserProvider>()` | Core + audit-user provider |
| `AddBlazorBaseCrudComponents()` | Interactive UI services the CRUD components depend on (`IConfirmationService`, default `FluentUiConfirmationService`, needs the FluentUI `IDialogService`) — Wasm ✓ / Maui ✓ / interactive, Server —; call after `AddFluentUIComponents()` |
| `AddBaseDbContext<TContext>(…)` | DbContext factory with interceptors |
| `AddBlazorBaseCrudServer<TContext>(Assembly?)` | Assembly scan → `DbContextBaseDataProvider<T>` |
| `AddBlazorBaseCrudClient(Action<HttpClient>, Assembly?)` | Assembly scan → `HttpBaseDataProvider<T>` |
| `AddBaseHttpDataProvider<T>(…)` | Manual HTTP provider registration |
| `AddBaseDataInterceptor<T,I>()` | Register lifecycle interceptor |
| `AddBaseValidator<T,V>()` | Register custom validator |
| `AddBlazorBaseCustomInput<TComponent>()` | Register a custom card input component (scoped; first matching `CanHandle` wins) |
| `AddBlazorBaseCustomDisplay<TComponent>()` | Register a custom list-cell display component (scoped; first matching `CanHandle` wins) |
| `MapBlazorBaseCrudEndpoints(Assembly?)` | Assembly scan → all CRUD endpoints |
| `MapBlazorBaseCrudEndpoints<T>(route, options?)` | Manual endpoint mapping |

### REST endpoints (per entity)

| Method | Route | Description |
|---|---|---|
| `POST` | `/api/base/<route>/query` | `BaseQuery` body → `BaseQueryResult<T>`. **403** if the caller lacks class-level `R`, or if a filter/sort targets a field the caller may not read (closes a field-value oracle). |
| `GET` | `/api/base/<route>/{id}` | `?select=Name,Price` optional |
| `GET` | `/api/base/<route>/count` | |
| `POST` | `/api/base/<route>` | Create |
| `PATCH` | `/api/base/<route>/{id}` | `PatchModel` body. **404** when `UserFilter` (below) excludes the row from the caller's scope. |
| `DELETE` | `/api/base/<route>/{id}` | **404** when `UserFilter` excludes the row from the caller's scope. |

`{id}` is parsed as `string`, `Guid`, `int` or `long` automatically.

**Page-size cap (CRUD-DA-05).** `BaseQuery.Take` is clamped server-side in `DbContextBaseDataProvider` to
`BaseQueryLimits.MaxPageSize` (global static, default `1000`; set `<= 0` to disable). This bounds an
unauthenticated/unbounded `query` (e.g. `Take = int.MaxValue`) into a DoS-safe page on **both** the REST
endpoint path and the direct Blazor-Server provider path. Normal pagination is unaffected; a host that
legitimately needs larger pages raises `BaseQueryLimits.MaxPageSize` once at startup (a non-positive
value keeps the default rather than disabling the cap; set `int.MaxValue` to effectively remove it).
`TotalCount` still reports the true unclamped total.
>
> **Residuals the cap does NOT cover:** it bounds only root-row cardinality. A `BaseQuery.Select` that
> names a *collection* navigation still materializes that child collection per root row, and a very large
> `Skip` still forces a deep `OFFSET` scan in the database. For untrusted-client paths, restrict which
> collection navigations may be selected and prefer keyset pagination over deep `Skip`; do not treat the
> page-size cap as a complete query-cost bound.

Response payloads for `query`, `GET {id}`, `POST` and `PATCH` are stripped of every field the caller may not `Read`, including reference and collection navigations whose target class (or the navigation itself) is not readable — see [Field security via attributes](#field-security-via-attributes).

Auth is required by default. Per-entity override, including row-level scoping via `UserFilter`:
```csharp
app.MapBlazorBaseCrudEndpoints<Product>("products", o =>
{
    o.RoutePrefix = "api/v2";
    o.RequireAuth = true;
    o.AuthorizationPolicy = "AdminOnly";
    o.UserFilter = user => p => p.OwnerId == user.FindFirst(ClaimTypes.NameIdentifier)!.Value;
});
```

`UserFilter` is a per-`ClaimsPrincipal` row predicate, enforced as a row-level-security boundary on `query`, `count` and `GET {id}` (passed through as the `scopeFilter` argument to `IBaseDataProvider<TModel>`), and — via a scoped existence pre-check — on `PATCH`/`DELETE`, which return **404** instead of leaking the existence of an out-of-scope row.

> **BREAKING:** The public property `BaseEndpointOptions.QueryCustomizer` has been removed (it was non-functional/dead code). To shape queries at the DI level, pass a `queryCustomizer` argument to the `DbContextBaseDataProvider<T>` constructor when registering it manually; to scope rows per user/request, use `UserFilter` above instead.

### Fluent Query Builder

```csharp
using BlazorBase.CRUD.Querying;

var result = await provider.Query()
    .Where(p => p.IsActive && p.Price > 10)
    .OrderBy(p => p.Name)
    .Select(p => p.Id, p => p.Name, p => p.Price)
    .Include<OrderItem>(p => p.Items, nav => nav
        .Where(i => i.IsActive)
        .OrderBy(i => i.DisplayOrder))
    .Take(50)
    .ToListAsync();

var current = await provider.Query()
    .Where(a => a.EndTime == null)
    .OrderByDescending(a => a.StartTime)
    .FirstOrDefaultAsync();
```

`Where()` decomposes lambda expressions into `FilterDescriptor` trees (supports `==`, `!=`, `>`, `<`, `>=`, `<=`, `Contains`, `StartsWith`, `EndsWith`, `IsNull`, `&&`, `||`, dotted paths, captured variables). Use `collection.Contains(e.Property)` to decompose to `FilterOperator.In` — translated to SQL `IN (...)` by the EF provider.

---

## DiffViewer Component

`BlazorBase.CRUD.Components.DiffViewer.DiffViewer` renders a `FileDiff` in three modes: Inline, Side-by-side, or New-only. Uses the vendored highlight.js 11.9.0 (common-languages build, BSD-3, no CDN) for optional syntax highlighting.

### Diff model namespace

The five diff types live in `BlazorBase.CRUD.Models.Diff` (moved from `DevPortal.Shared.Modules.Repositories.Diff`; JSON wire format unchanged):

```csharp
namespace BlazorBase.CRUD.Models.Diff;

public record FileDiff(string Path, string? OldPath, FileChangeKind ChangeKind, bool IsBinary, IReadOnlyList<DiffHunk> Hunks);
public record DiffHunk(string Header, int OldStart, int OldLines, int NewStart, int NewLines, IReadOnlyList<DiffLine> Lines);
public record DiffLine(DiffLineKind Kind, int? OldLineNumber, int? NewLineNumber, string Content);
public enum DiffLineKind { Context, Added, Removed }
public enum FileChangeKind { Added, Modified, Deleted, Renamed, Copied, TypeChanged }
```

### Key parameters

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `Diff` | `FileDiff?` | `null` | Null / no hunks → `EmptyDiffLabel` |
| `Mode` | `DiffViewMode` | `Inline` | `Inline`, `SideBySide`, `NewOnly`. Two-way bindable. |
| `ShowModeSwitch` | `bool` | `true` | Renders the mode-switch toolbar |
| `Language` | `string?` | `null` | highlight.js language hint. Null = auto-detect. |
| `EnableSyntaxHighlight` | `bool` | `true` | Applies highlight.js after render |
| `MaxRenderedLines` | `int` | `5000` | Line cap; shows `LargeDiffLabel` when exceeded |
| `FilePath` | `string?` | `null` | File path carried into every `DiffLineCommentContext`; purely informational for the host. |
| `LineCommentTemplate` | `RenderFragment<DiffLineCommentContext>?` | `null` | When set, an extra full-width row is rendered immediately below every diff line. The host controls content — return empty for lines without a thread. Null = no extra rows (default). |
| `OnAddLineComment` | `EventCallback<DiffLineCommentContext>` | — | When set, a gutter "+" button appears on each line (visible on hover/focus). Not set = no button (default). |
| `AddCommentLabel` | `string` | `"Add comment"` | `aria-label` for the per-line gutter button. |

### `DiffLineCommentContext` record

```csharp
namespace BlazorBase.CRUD.Components.DiffViewer;

public record DiffLineCommentContext(
    string? FilePath,
    int? OldLineNumber,
    int? NewLineNumber,
    DiffLineKind Kind);
```

Passed to `OnAddLineComment` and `LineCommentTemplate`. `FilePath` echoes the host-supplied `DiffViewer.FilePath`. `OldLineNumber`/`NewLineNumber` are null when the line has no position on that side. Works identically across all three view modes.

### Backward compatibility

All three new parameters default to no-ops (`null` / no delegate). Existing call sites that do not supply them are visually identical to before — no gutter column, no extra rows.

### Line-comment usage pattern

The host renders the template for every line; return empty content for lines without a thread. The `DiffViewer` does not filter by whether a thread exists — that keeps the component generic and the host in full control.

```razor
<DiffViewer Diff="@myFileDiff"
            FilePath="@selectedFile"
            OnAddLineComment="@HandleAddComment"
            LineCommentTemplate="@RenderLineThread"
            AddCommentLabel="@Localizer["AddComment"]" />

@code {
    private RenderFragment<DiffLineCommentContext> RenderLineThread => ctx =>
        @<CommentThread FilePath="@ctx.FilePath"
                        Line="@(ctx.NewLineNumber ?? ctx.OldLineNumber)"
                        Threads="@GetThreadsForLine(ctx)" />;
}
```

### XSS safety

Content is bound as Razor text (HTML-encoded by Blazor). The `[data-code]` attribute marks cells for the JS module, which reads `textContent` and passes it to `hljs.highlight()` — which itself escapes its input before emitting `<span>` wrappers. No server-provided HTML is ever injected as markup.

### highlight.js vendored assets

| File | Description |
|---|---|
| `wwwroot/lib/highlight/highlight.min.js` | Common-languages build (121 KB) — highlight.js 11.9.0 |
| `wwwroot/lib/highlight/github-dark.min.css` | Dark theme |
| `wwwroot/lib/highlight/LICENSE` | BSD-3-Clause license |

The `github-dark.min.css` theme is injected once into `<head>` by `syntaxHighlight.js` — no consumer-side `<link>` tag required. The JS module path is `./_content/BlazorBase.CRUD/js/syntaxHighlight.js`.

### Usage

```razor
<DiffViewer Diff="@myFileDiff" Language="csharp" />
<DiffViewer Diff="@myFileDiff" @bind-Mode="@CurrentMode" EnableSyntaxHighlight="false" />
```

---

## FileTree Component

`BlazorBase.CRUD.Components.FileTree.FileTree` renders an ARIA-compliant navigable file tree. Build the node hierarchy from a flat path list with `FileTreeBuilder.Build(paths)`.

### Models (`BlazorBase.CRUD.Models.FileTree`)

```csharp
public sealed class FileTreeNode
{
    public required string Name { get; init; }   // last path segment
    public required string Path { get; init; }   // full '/'-separated path from root
    public bool IsDirectory { get; init; }
    public IReadOnlyList<FileTreeNode> Children { get; init; } = [];
}

public static class FileTreeBuilder
{
    // Flat '/'-separated path list → node hierarchy (dirs first, name-sorted per level)
    public static IReadOnlyList<FileTreeNode> Build(IEnumerable<string> paths);
}
```

### Key parameters

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `Nodes` | `IReadOnlyList<FileTreeNode>` | `[]` | Root nodes |
| `SelectedPath` | `string?` | `null` | Two-way bindable |
| `SelectedPathChanged` | `EventCallback<string>` | — | Raised on file selection |
| `OnNodeSelected` | `EventCallback<FileTreeNode>` | — | Raised with the full node |
| `ExpandRootByDefault` | `bool` | `true` | Root nodes start expanded |
| `EmptyLabel` | `string` | `"No files"` | Shown when Nodes is empty |

ARIA: `role="tree"` on root, `role="treeitem"` + `aria-expanded` + `aria-selected` on each node, `role="group"` on children. Roving tabindex: the focused node holds `tabindex="0"`; all others hold `tabindex="-1"`. Keyboard (WAI-ARIA tree pattern): ArrowDown/ArrowUp move focus to the next/previous visible node and call `FocusAsync()`; ArrowRight expands a collapsed directory, or moves focus to its first child when already expanded; ArrowLeft collapses an expanded directory, or moves focus to the parent node; Enter/Space select a file or toggle a directory.

### Usage

```razor
<FileTree Nodes="@TreeNodes"
          @bind-SelectedPath="@SelectedPath"
          OnNodeSelected="@HandleFileSelected" />
```

---

## Code Examples

### Declarative list + dialog edit

```razor
<BaseList TModel="Product" AllowAdd="true" AllowEdit="true">
    <BaseColumn TModel="Product" Property="@(x => x.Name)" Title="Name" />
    <BaseColumn TModel="Product" Property="@(x => x.Price)" Title="Price" Format="C2" Width="120px" />
</BaseList>
```

### Fluent list + card configuration

```csharp
public partial class ProductList : ComponentBase
{
    private BaseListConfiguration<Product> ListConfig { get; } =
        new BaseListBuilder<Product>()
            .Column(x => x.Name,  c => c.Title("Name").Sortable().Filterable())
            .Column(x => x.Price, c => c.Title("Price").Format("C2").Width("120px"))
            .Build();

    private BaseCardConfiguration<Product> CardConfig { get; } =
        new BaseCardBuilder<Product>()
            .Field(x => x.Name,        f => f.Label("Name").Required())
            .Field(x => x.Price,       f => f.Label("Price").Required())
            .Field(x => x.Description, f => f.Label("Description").Lines(4).ColSpan(2))
            .Build();
}
```

```razor
<BaseList TModel="Product"
          Configuration="@ListConfig"
          CardConfiguration="@CardConfig"
          AllowAdd="true" />
```

### Field security via attributes

```csharp
[BaseCrud("products")]
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    [CrudAccess("Admin", "RM")]
    [CrudAccess("Manager", "R")]
    public decimal CostPrice { get; set; }

    [CrudAccess("*", "R")]
    [CrudAccess("Admin", "RM")]
    public decimal RetailPrice { get; set; }

    [CrudAccess("*", "")]
    public string InternalNotes { get; set; } = string.Empty;
}
```

- Class-level `[CrudAccess]` gates whole operations: `R` = list/get, `I` = create, `M` = update, `D` = delete.
- Property-level `[CrudAccess]` strips fields without `R` from responses, rejects PATCHes for fields without `M` (403) and POST bodies that set fields without `I` (403).
- `[CrudAccess("*", "")]` matches everyone with zero rights — equivalent to the old `[CrudIgnore]` (PATCH returns 400 if the field is included).
- Hierarchy: Class > Property > View — each level only further restricts via intersection.
- Response field-stripping recurses into reference and collection navigations — a navigation whose target class (or the navigation itself) is not readable is nulled out (or its elements stripped) exactly like a scalar property.
- `POST /query` additionally returns **403** when a filter or sort targets a field the caller may not read, so the query surface cannot be used as an oracle to infer non-readable values.
- Row-level scoping (`scopeFilter` on `IBaseDataProvider<TModel>`, configured per endpoint via `BaseEndpointOptions.UserFilter`) sits on top of field security: enforced by the server `DbContextBaseDataProvider`, ignored by the client `HttpBaseDataProvider`, and rejected with `NotSupportedException` by the Identity-backed `UserDataProvider`.

### Interceptor for server-side rules

```csharp
public class ProductInterceptor : BaseDataInterceptor<Product>
{
    public override Task<Product> OnBeforeCreateAsync(Product model, CancellationToken ct = default)
    {
        model.Name = model.Name.Trim();
        return Task.FromResult(model);
    }

    public override Task<BaseQuery> OnBeforeQueryAsync(BaseQuery query, CancellationToken ct = default)
    {
        if (query.Sorts.Count == 0)
            query.Sorts.Add(new SortDescriptor { PropertyName = "Name", Direction = SortDirection.Ascending });

        return Task.FromResult(query);
    }
}
```

```csharp
builder.Services.AddBaseDataInterceptor<Product, ProductInterceptor>();
```

### Custom validator

```csharp
public class ProductValidator(IBaseDataProvider<Product> products) : IBaseValidator<Product>
{
    private readonly IBaseDataProvider<Product> Products = products;

    public async Task<IEnumerable<ValidationResult>> ValidateAsync(Product model, CancellationToken ct = default)
    {
        var existing = await Products.GetListAsync(new BaseQuery
        {
            Filters = [new FilterDescriptor { PropertyName = "Name", Operator = FilterOperator.Equals, Value = model.Name }],
        }, ct: ct);

        if (existing.Items.Any(p => p.Id != model.Id))
            return [new ValidationResult("Name already in use.", [nameof(model.Name)])];

        return [];
    }
}
```

```csharp
builder.Services.AddBaseValidator<Product, ProductValidator>();
```

### Custom action — bulk activate

```csharp
new BaseListBuilder<Product>()
    .Column(x => x.Name)
    .ActionGroup("Lifecycle", g => g
        .Caption("Actions")
        .Icon(new Icons.Regular.Size20.Play())
        .Action("Activate selected", a => a
            .Icon(new Icons.Regular.Size16.CheckmarkCircle())
            .ShowIn(CrudActionContext.ListToolbar)
            .BulkAction()
            .Visible(args => Task.FromResult(args.Item?.IsActive != true))
            .VisibleForRoles(user => user.IsInRole("Admin"))
            .OnExecute(async args =>
            {
                foreach (var item in args.SelectedItems)
                    await ActivateAsync(item);
            })))
    .Build();
```

### Filtered include with the fluent builder

```csharp
var orders = await orderProvider.Query()
    .Where(o => o.OrderDate >= from)
    .OrderByDescending(o => o.OrderDate)
    .Include<OrderItem>(o => o.Items, nav => nav
        .Where(i => i.Quantity > 0)
        .OrderBy(i => i.LineNumber))
    .Take(100)
    .ToListAsync();
```

Generates EF Core's filtered include:
```csharp
.Include(o => o.Items.Where(i => i.Quantity > 0).OrderBy(i => i.LineNumber))
```

---

## Full Razor Page Examples

The previous section showed isolated snippets. This section shows complete `.razor` + `.razor.cs` files following the project's conventions: file-scoped namespaces, primary-constructor DI, `private readonly` fields inside a `#region Injects` block, no `@code` blocks in Razor, no inline styles.

### Example 1 — Minimal declarative list

The simplest possible setup: a single `BaseList` with declarative `BaseColumn` children. No code-behind logic — `IBaseDataProvider<Product>` is resolved from DI automatically, the edit dialog opens on row click.

**`ProductList.razor`**
```razor
@page "/products"
@attribute [Authorize]
@using BlazorBase.CRUD.Components
@using MyApp.Shared.Modules.Catalog.Models

<div>
    <FluentLabel Typo="Typography.PageTitle">@Localizer["Title"]</FluentLabel>

    <BaseList TModel="Product" AllowAdd="true" AllowEdit="true" AllowDelete="true">
        <BaseColumn TModel="Product" Property="@(p => p.Name)"  Title="@Localizer["Name"]" />
        <BaseColumn TModel="Product" Property="@(p => p.Price)" Title="@Localizer["Price"]" Format="C2" Width="120px" />
        <BaseColumn TModel="Product" Property="@(p => p.CategoryId)" Title="@Localizer["Category"]" />
    </BaseList>
</div>
```

**`ProductList.razor.cs`**
```csharp
using Microsoft.Extensions.Localization;

namespace MyApp.Shared.Modules.Catalog.Pages;

public partial class ProductList(IStringLocalizer<ProductList> localizer)
{
    #region Injects
    private readonly IStringLocalizer<ProductList> Localizer = localizer;
    #endregion
}
```

Notes:
- `BaseColumn`'s `Property` selector controls both the displayed value and the column's sortable/filterable behavior.
- The `CategoryId` column will show the linked `Category` if a navigation is detected on the entity (FK convention).
- Resource keys (`Title`, `Name`, `Price`, …) come from `ProductList.resx` / `ProductList.en.resx` next to the Razor file.

### Example 2 — List + Card via fluent builder (separate edit dialog)

The list and the edit form share one model. The fluent builder lives in the code-behind, keeping markup minimal and re-usable.

**`OrderList.razor`**
```razor
@page "/orders"
@attribute [Authorize]
@using BlazorBase.CRUD.Components
@using MyApp.Shared.Modules.Sales.Models

<div>
    <FluentLabel Typo="Typography.PageTitle">@Localizer["Title"]</FluentLabel>

    <BaseList TModel="Order"
              Configuration="@ListConfig"
              CardConfiguration="@CardConfig"
              AllowAdd="true" AllowEdit="true" AllowDelete="true" />
</div>
```

**`OrderList.razor.cs`**
```csharp
using BlazorBase.CRUD.Configuration;
using Microsoft.Extensions.Localization;
using MyApp.Shared.Modules.Sales.Models;

namespace MyApp.Shared.Modules.Sales.Pages;

public partial class OrderList(IStringLocalizer<OrderList> localizer)
{
    #region Injects
    private readonly IStringLocalizer<OrderList> Localizer = localizer;
    #endregion

    private BaseListConfiguration<Order> ListConfig { get; } =
        new BaseListBuilder<Order>()
            .Column(o => o.OrderNumber,  c => c.Title("Order #").Width("120px"))
            .Column(o => o.CustomerName, c => c.Title("Customer").Sortable().Filterable())
            .Column(o => o.OrderDate,    c => c.Title("Date").Format("yyyy-MM-dd").Width("140px"))
            .Column(o => o.TotalAmount,  c => c.Title("Total").Format("C2").Width("120px"))
            .Build();

    private BaseCardConfiguration<Order> CardConfig { get; } =
        new BaseCardBuilder<Order>()
            .Group("General", g => g
                .Field(o => o.OrderNumber,  f => f.Label("Order #").Required())
                .Field(o => o.CustomerName, f => f.Label("Customer").Required())
                .Field(o => o.OrderDate,    f => f.Label("Order Date").Required()))
            .Group("Amounts", g => g
                .Field(o => o.TotalAmount,  f => f.Label("Total").Required())
                .Field(o => o.Currency,     f => f.Label("Currency")))
            .Group("Notes", g => g
                .Field(o => o.Notes, f => f.Label("Notes").Lines(4).ColSpan(2)))
            .Build();
}
```

When the user clicks any row in the list, a `BaseDialog<Order>` opens carrying `CardConfig`. The "Add" button reuses the same dialog with an empty model.

### Example 3 — Master/Detail with parent filter and bound creation

A common pattern: the upper list shows parents (`ActivityType`), and selecting a parent shows the related children (`ActivitySubtype`) below. The child list is filtered by parent id, and newly created children inherit the parent FK automatically.

**`ActivityTypeManagement.razor`**
```razor
@page "/admin/activity-types"
@attribute [Authorize(Roles = "Admin")]
@using BlazorBase.CRUD.Components
@using BlazorBase.CRUD.Models
@using MyApp.Shared.Modules.ActivityTracker.Models

<div>
    <FluentLabel Typo="Typography.PageTitle">@Localizer["Title"]</FluentLabel>

    <BaseList @ref="TypeList" TModel="ActivityType"
              AllowDelete="false"
              AddButtonText="@Localizer["CreateType"]"
              EmptyText="@Localizer["NoTypesFound"]"
              OnAfterItemCreated="OnTypeChanged"
              OnAfterItemUpdated="OnTypeChanged">
        <ChildContent>
            <BaseColumn TModel="ActivityType" Property="@(t => t.Color)" Title="@Localizer["Color"]"
                        Sortable="false" Filterable="false">
                <Template>
                    <div class="color-dot" style="--type-color: @context.Color"></div>
                </Template>
            </BaseColumn>
            <BaseColumn TModel="ActivityType" Property="@(t => t.Name)" Title="@Localizer["Name"]" />
            <BaseColumn TModel="ActivityType" Property="@(t => t.DisplayOrder)" Title="@Localizer["DisplayOrder"]" />
        </ChildContent>
        <ContextMenuActions>
            <div role="menuitem" @onclick="() => ShowSubtypes(context)">
                <FluentIcon Value="@(new Icons.Regular.Size16.List())" />
                <span>@Localizer["ManageSubtypes"]</span>
            </div>
        </ContextMenuActions>
    </BaseList>

    @if (SelectedType is not null)
    {
        <FluentDivider Class="section-divider" />

        <FluentLabel Typo="Typography.Header">
            @string.Format(Localizer["SubtypesOf"], SelectedType.Name)
        </FluentLabel>

        <BaseList @ref="SubtypeList" TModel="ActivitySubtype"
                  AdditionalFilters="SubtypeFilters"
                  AllowDelete="false"
                  AddButtonText="@Localizer["CreateSubtype"]"
                  OnBeforeItemCreate="OnBeforeSubtypeCreate">
            <ChildContent>
                <BaseColumn TModel="ActivitySubtype" Property="@(s => s.Name)" Title="@Localizer["Name"]" />
                <BaseColumn TModel="ActivitySubtype" Property="@(s => s.DisplayOrder)" Title="@Localizer["DisplayOrder"]" />
            </ChildContent>
        </BaseList>
    }
</div>
```

**`ActivityTypeManagement.razor.cs`**
```csharp
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Models;
using Microsoft.Extensions.Localization;
using MyApp.Shared.Modules.ActivityTracker.Models;

namespace MyApp.Shared.Modules.ActivityTracker.Pages;

public partial class ActivityTypeManagement(IStringLocalizer<ActivityTypeManagement> localizer)
{
    #region Injects
    private readonly IStringLocalizer<ActivityTypeManagement> Localizer = localizer;
    #endregion

    private BaseList<ActivityType>? TypeList;
    private BaseList<ActivitySubtype>? SubtypeList;
    private ActivityType? SelectedType;
    private List<FilterDescriptor>? SubtypeFilters;

    private void ShowSubtypes(ActivityType type)
    {
        SelectedType = type;
        SubtypeFilters =
        [
            new FilterDescriptor
            {
                PropertyName = nameof(ActivitySubtype.ActivityTypeId),
                Operator = FilterOperator.Equals,
                Value = type.Id.ToString(),
            },
        ];
    }

    private Task OnTypeChanged(ActivityType type)
    {
        if (SelectedType is not null && SelectedType.Id == type.Id)
            SelectedType = type;

        return Task.CompletedTask;
    }

    private Task OnBeforeSubtypeCreate(ItemEventArgs<ActivitySubtype> args)
    {
        if (SelectedType is null)
        {
            args.Cancel = true;
            return Task.CompletedTask;
        }

        args.Item.ActivityTypeId = SelectedType.Id;
        return Task.CompletedTask;
    }
}
```

Key points:
- `AdditionalFilters` is a server-side filter applied to every list query — so the child list always asks for "this parent's children".
- `OnBeforeItemCreate` lets you stamp the FK on a newly added child before it's POSTed.
- `@ref="TypeList"` exposes the `BaseList<T>` so its `RefreshAsync()`, `SelectedItems`, etc. can be called from C#.

### Example 4 — Custom column templates (badges, formatters, icons)

`<Template>` inside `BaseColumn` overrides the rendered cell. `context` is the row entity.

**`ActivityHistory.razor`**
```razor
@page "/activity-tracker/history"
@attribute [Authorize]
@using BlazorBase.CRUD.Components
@using MyApp.Shared.Modules.ActivityTracker.Models

<div>
    <FluentLabel Typo="Typography.PageTitle">@Localizer["Title"]</FluentLabel>

    <BaseList @ref="ActivityList" TModel="Activity"
              AdditionalFilters="DateFilters"
              AllowAdd="false" AllowEdit="false" AllowDelete="true">
        <ChildContent>
            <BaseColumn TModel="Activity" Property="@(a => a.ActivityType.Color)" Title="@Localizer["Color"]"
                        Sortable="false" Filterable="false">
                <Template>
                    <div class="color-dot" style="--type-color: @context.ActivityType?.Color"></div>
                </Template>
            </BaseColumn>

            <BaseColumn TModel="Activity" Property="@(a => a.ActivityType.Name)" Title="@Localizer["Type"]" />

            <BaseColumn TModel="Activity" Property="@(a => a.StartTime)" Title="@Localizer["Start"]">
                <Template>
                    @context.StartTime.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
                </Template>
            </BaseColumn>

            <BaseColumn TModel="Activity" Property="@(a => a.EndTime)" Title="@Localizer["End"]">
                <Template>
                    @if (context.EndTime is { } end)
                    {
                        @end.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
                    }
                    else
                    {
                        <FluentBadge Appearance="Appearance.Accent">@Localizer["Running"]</FluentBadge>
                    }
                </Template>
            </BaseColumn>
        </ChildContent>
    </BaseList>
</div>
```

**`ActivityHistory.razor.cs`**
```csharp
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Models;
using Microsoft.Extensions.Localization;
using MyApp.Shared.Modules.ActivityTracker.Models;

namespace MyApp.Shared.Modules.ActivityTracker.Pages;

public partial class ActivityHistory(IStringLocalizer<ActivityHistory> localizer)
{
    #region Injects
    private readonly IStringLocalizer<ActivityHistory> Localizer = localizer;
    #endregion

    private BaseList<Activity>? ActivityList;

    private List<FilterDescriptor> DateFilters { get; } =
    [
        new() { PropertyName = nameof(Activity.StartTime), Operator = FilterOperator.GreaterThanOrEqual, Value = DateTime.Today.AddDays(-7) },
    ];
}
```

CSS for the color dot lives in `ActivityHistory.razor.css` and uses a CSS variable so the value can be data-driven:

```css
.color-dot {
    width: 12px;
    height: 12px;
    border-radius: 50%;
    background: var(--type-color);
}
```

### Example 5 — Stand-alone BaseCard (custom edit page)

If you don't want the auto-edit dialog (e.g. for a wizard or full-page edit), embed `BaseCard` directly. The card resolves `IBaseDataProvider<Product>` from DI and tracks dirty fields automatically.

**`ProductEdit.razor`**
```razor
@page "/products/{Id:guid}/edit"
@attribute [Authorize]
@using BlazorBase.CRUD.Components
@using MyApp.Shared.Modules.Catalog.Models

<div>
    <FluentLabel Typo="Typography.PageTitle">@Localizer["Title"]</FluentLabel>

    @if (Model is null)
    {
        <FluentProgressRing />
    }
    else
    {
        <BaseCard TModel="Product"
                  Model="@Model"
                  Configuration="@CardConfig"
                  MaxColumns="2"
                  OnAfterSave="@OnSaved" />
    }
</div>
```

**`ProductEdit.razor.cs`**
```csharp
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MyApp.Shared.Modules.Catalog.Models;

namespace MyApp.Shared.Modules.Catalog.Pages;

public partial class ProductEdit(
    IStringLocalizer<ProductEdit> localizer,
    IBaseDataProvider<Product> products,
    NavigationManager navigation)
{
    #region Injects
    private readonly IStringLocalizer<ProductEdit> Localizer = localizer;
    private readonly IBaseDataProvider<Product> Products = products;
    private readonly NavigationManager Navigation = navigation;
    #endregion

    [Parameter] public Guid Id { get; set; }

    private Product? Model;

    private BaseCardConfiguration<Product> CardConfig { get; } =
        new BaseCardBuilder<Product>()
            .Field(p => p.Name,        f => f.Label("Name").Required())
            .Field(p => p.Price,       f => f.Label("Price").Required())
            .Field(p => p.CategoryId,  f => f.Label("Category").Required())
            .Field(p => p.Description, f => f.Label("Description").Lines(4).ColSpan(2))
            .Build();

    protected override async Task OnInitializedAsync()
    {
        Model = await Products.GetByIdAsync(Id);
    }

    private Task OnSaved(Product saved)
    {
        Navigation.NavigateTo("/products");
        return Task.CompletedTask;
    }
}
```

### Example 6 — Embedded child collection with `ListPartField`

`BaseCard` automatically renders `ICollection<T>` navigation properties as embedded grids. Use `ListPartField` to customize them. Below an `Order` is edited together with its `OrderItem` lines in one card. The optional `ChildCardType` points at a separate card component used for the per-item edit dialog (the markup analog of `CardType` on `BaseList`); it is hosted in defer-save mode and must accept `Model`, `Localizer`, `DeferSave`, `OnDeferredSave` and `OnCancelled`. Omit it to fall back to the auto-generated child card.

**`OrderEdit.razor`**
```razor
@page "/orders/{Id:guid}/edit"
@attribute [Authorize]
@using BlazorBase.CRUD.Components
@using MyApp.Shared.Modules.Sales.Models

<div>
    @if (Model is null)
    {
        <FluentProgressRing />
    }
    else
    {
        <BaseCard TModel="Order" Model="@Model" MaxColumns="2">
            <PropertyField TModel="Order" Property="@(o => o.OrderNumber)"  Label="@Localizer["OrderNumber"]" Required="true" />
            <PropertyField TModel="Order" Property="@(o => o.CustomerName)" Label="@Localizer["Customer"]"    Required="true" />
            <PropertyField TModel="Order" Property="@(o => o.OrderDate)"    Label="@Localizer["OrderDate"]"   Required="true" />
            <PropertyField TModel="Order" Property="@(o => o.Notes)"        Label="@Localizer["Notes"]"       Lines="3" ColSpan="2" />

            <ListPartField TModel="Order"
                           Property="@(o => o.Items)"
                           AllowAdd="true"
                           AllowDelete="true"
                           AllowReorder="true"
                           ChildCardType="typeof(OrderItemCard)" />
        </BaseCard>
    }
</div>
```

**`OrderEdit.razor.cs`**
```csharp
using BlazorBase.CRUD.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MyApp.Shared.Modules.Sales.Models;

namespace MyApp.Shared.Modules.Sales.Pages;

public partial class OrderEdit(IStringLocalizer<OrderEdit> localizer, IBaseDataProvider<Order> orders)
{
    #region Injects
    private readonly IStringLocalizer<OrderEdit> Localizer = localizer;
    private readonly IBaseDataProvider<Order> Orders = orders;
    #endregion

    [Parameter] public Guid Id { get; set; }

    private Order? Model;

    protected override async Task OnInitializedAsync()
    {
        Model = await Orders.GetByIdAsync(Id, select: ["OrderNumber", "CustomerName", "OrderDate", "Notes", "Items"]);
    }
}
```

`select` limits the over-the-wire payload to exactly what the card binds — useful for wide entities. Navigation collections referenced in `select` (`Items`) are eager-loaded via EF Core `.Include()`.

### Example 7 — Toolbar action + bulk action via fluent builder

Combines a single-item action ("Duplicate") and a bulk action ("Archive selected"). Visibility is role-based; the bulk action's toolbar button auto-disables until rows are selected.

**`ProductManagement.razor`**
```razor
@page "/admin/products"
@attribute [Authorize(Roles = "Admin")]
@using BlazorBase.CRUD.Components
@using MyApp.Shared.Modules.Catalog.Models

<div>
    <FluentLabel Typo="Typography.PageTitle">@Localizer["Title"]</FluentLabel>

    <BaseList TModel="Product"
              Configuration="@ListConfig"
              AllowAdd="true" AllowEdit="true" AllowDelete="true" />
</div>
```

**`ProductManagement.razor.cs`**
```csharp
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using MyApp.Shared.Modules.Catalog.Models;

namespace MyApp.Shared.Modules.Catalog.Pages;

public partial class ProductManagement(
    IStringLocalizer<ProductManagement> localizer,
    IBaseDataProvider<Product> products)
{
    #region Injects
    private readonly IStringLocalizer<ProductManagement> Localizer = localizer;
    private readonly IBaseDataProvider<Product> Products = products;
    #endregion

    private BaseListConfiguration<Product> ListConfig => new BaseListBuilder<Product>()
        .Column(p => p.Name,   c => c.Title(Localizer["Name"]).Sortable().Filterable())
        .Column(p => p.Price,  c => c.Title(Localizer["Price"]).Format("C2"))
        .Column(p => p.IsActive, c => c.Title(Localizer["Active"]))
        .ActionGroup("Lifecycle", g => g
            .Caption(Localizer["Actions"])
            .Icon(new Icons.Regular.Size20.MoreHorizontal())
            .Action("duplicate", a => a
                .Caption(Localizer["Duplicate"])
                .Icon(new Icons.Regular.Size16.Copy())
                .ShowIn(CrudActionContext.ContextMenu | CrudActionContext.ListToolbar)
                .Visible(args => Task.FromResult(args.Item is not null))
                .OnExecute(args => DuplicateAsync(args.Item!)))
            .Action("archive", a => a
                .Caption(Localizer["ArchiveSelected"])
                .Icon(new Icons.Regular.Size16.Archive())
                .ShowIn(CrudActionContext.ListToolbar)
                .BulkAction()
                .VisibleForRoles(user => user.IsInRole("Admin"))
                .OnExecute(args => ArchiveAsync(args.SelectedItems))))
        .Build();

    private async Task DuplicateAsync(Product source)
    {
        var copy = new Product
        {
            Name        = $"{source.Name} (Copy)",
            Price       = source.Price,
            CategoryId  = source.CategoryId,
            Description = source.Description,
        };

        await Products.CreateAsync(copy);
    }

    private async Task ArchiveAsync(IReadOnlyCollection<Product> items)
    {
        foreach (var item in items)
            await Products.PatchAsync(item.Id, new() { [nameof(Product.IsActive)] = false });
    }
}
```

### Example 8 — Reading `BaseList` state from C#

Sometimes a parent page needs to react to selection changes or refresh the list manually. Use `@ref` plus the events.

**`InventoryPage.razor`**
```razor
@page "/inventory"
@attribute [Authorize]
@using BlazorBase.CRUD.Components
@using MyApp.Shared.Modules.Inventory.Models

<div>
    <BaseList @ref="ItemList" TModel="InventoryItem"
              OnItemSelected="OnItemSelected"
              OnAfterItemUpdated="@(_ => RefreshSummaryAsync())">
        <BaseColumn TModel="InventoryItem" Property="@(i => i.Sku)"    Title="SKU" />
        <BaseColumn TModel="InventoryItem" Property="@(i => i.Name)"   Title="Name" />
        <BaseColumn TModel="InventoryItem" Property="@(i => i.Stock)"  Title="Stock" />
    </BaseList>

    <FluentDivider />

    <FluentLabel>@Summary</FluentLabel>
</div>
```

**`InventoryPage.razor.cs`**
```csharp
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Core;
using MyApp.Shared.Modules.Inventory.Models;

namespace MyApp.Shared.Modules.Inventory.Pages;

public partial class InventoryPage(IBaseDataProvider<InventoryItem> items)
{
    #region Injects
    private readonly IBaseDataProvider<InventoryItem> Items = items;
    #endregion

    private BaseList<InventoryItem>? ItemList;
    private string Summary = "";

    private Task OnItemSelected(InventoryItem item)
    {
        Summary = $"Selected: {item.Sku} — stock {item.Stock}";
        return Task.CompletedTask;
    }

    private async Task RefreshSummaryAsync()
    {
        var total = await Items.GetCountAsync();
        Summary = $"{total} items in stock";

        if (ItemList is not null)
            await ItemList.RefreshAsync();
    }
}
```

### Recurring Conventions

- **Markup-only `.razor`.** All logic is in the `.razor.cs` partial. No `@code` blocks.
- **Primary-constructor DI.** All injected services flow through primary-constructor parameters into `private readonly` fields in a `#region Injects` block.
- **One `BaseList`/`BaseCard` per concern.** When the same model needs multiple presentations, build several `BaseListConfiguration<T>` / `BaseCardConfiguration<T>` in code-behind rather than duplicating markup.
- **Templates over post-processing.** Prefer a `<Template>` inside a `BaseColumn` over loading raw data and rendering it elsewhere — the data binding stays declarative.
- **`AdditionalFilters` + `OnBeforeItemCreate`** is the canonical master/detail pattern: server-side filter for reads, FK injection on create.
- **CSS via Blazor isolation.** Component-specific styling lives in `ComponentName.razor.css`; use `style="--var: …"` only to pipe a data value to a CSS custom property.

---

## RichTextEditor Component

`BlazorBase.CRUD.Components.RichTextEditor.RichTextEditor` is a generic, vendored WYSIWYG rich-text editor that edits and produces an **HTML string**. It has no external dependencies and loads no CDN resources — the JS module ships as a static web asset of `BlazorBase.CRUD`.

### Parameters

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `Value` | `string?` | `null` | Current HTML value. Two-way bindable via `@bind-Value`. |
| `ValueChanged` | `EventCallback<string?>` | — | Raised ~200 ms after the user stops typing. |
| `Placeholder` | `string?` | `null` | Placeholder text shown in the empty editor surface. |
| `ReadOnly` | `bool` | `false` | Disables the contenteditable surface and toolbar via CSS + `contenteditable=false`. |
| `Disabled` | `bool` | `false` | Visual-only disabled state; also sets the surface read-only. |
| `Label` | `string?` | `null` | Optional label rendered above the toolbar. |
| `BoldLabel` | `string` | `"Bold"` | `title` and `aria-label` for the Bold toolbar button. |
| `ItalicLabel` | `string` | `"Italic"` | `title` and `aria-label` for the Italic toolbar button. |
| `UnderlineLabel` | `string` | `"Underline"` | `title` and `aria-label` for the Underline toolbar button. |
| `BulletListLabel` | `string` | `"Bulleted list"` | `title` and `aria-label` for the Bulleted list button. |
| `NumberedListLabel` | `string` | `"Numbered list"` | `title` and `aria-label` for the Numbered list button. |
| `InsertLinkLabel` | `string` | `"Insert link"` | `title` and `aria-label` for the Insert link button. |
| `ClearFormattingLabel` | `string` | `"Clear formatting"` | `title` and `aria-label` for the Clear formatting button. |
| `InsertLinkPromptText` | `string` | `"Enter URL:"` | Text shown in the browser prompt when Insert link is activated. |

The component implements `IAsyncDisposable` and cleans up its JS editor instance on disposal. The element id (`rte-{Guid:N}`) is stable per component instance — multiple editors coexist safely on the same page.

### Toolbar accessibility

Toolbar buttons carry both `title` and `aria-label` bound from the parameters above. Keyboard activation (Tab to a button + Enter or Space) is fully supported: `mousedown` on a toolbar button only calls `preventDefault()` to preserve the editor selection; the actual formatting command fires on `click`, which is raised by both mouse and keyboard. The root element exposes `aria-disabled` reflecting `ReadOnly` or `Disabled`.

### Link scheme validation

When the user activates Insert link, the prompted URL is validated against an allow-list of `http:`, `https:`, and `mailto:` schemes (case-insensitive). Any other scheme — including `javascript:` and `data:` — is silently rejected.

### Value re-sync

After the editor is initialized, external changes to `Value` (e.g. programmatic model updates) are pushed into the editor DOM via `setHtml`. A `LastSyncedValue` field prevents echoing changes that originated from the editor itself.

### Toolbar

Bold · Italic · Underline · Bulleted list · Numbered list · Insert link · Clear formatting — all via `document.execCommand`. The toolbar implementation is replaceable: the JS module exports a clean contract (`initEditor` / `setHtml` / `getHtml` / `setReadOnly` / `destroyEditor`) so a richer vendored library can be swapped behind it without changing the Blazor component.

### Theming

The component uses CSS isolation (`RichTextEditor.razor.css`). Colors and radii come from FluentUI CSS custom properties (`--neutral-layer-1`, `--neutral-foreground-rest`, `--accent-stroke-control-rest`, `--control-corner-radius`, …) so the editor automatically follows the host app's theme.

### Read-only / disabled state

When `ReadOnly` or `Disabled` is true, the CSS class `rte-disabled` is added to the root element. The stylesheet rule `.rte-disabled .rte-toolbar { pointer-events: none }` disables toolbar interaction via CSS — no imperative JS style writes. The surface `contenteditable` attribute is also set to `false`. The root element carries `aria-disabled="true"`.

### Sanitization

The `RichTextEditor` does **not** sanitize HTML. Raw HTML produced by the editor must be sanitized **server-side** before persistence. Use `IHtmlSanitizer` (see below) in an `IBaseDataInterceptor<T>` on the server. Display of stored HTML should use `SanitizedHtml` (see below).

### Usage — two-way bind

```razor
<RichTextEditor @bind-Value="Model.Body"
                Placeholder="Enter description…"
                Label="Description" />
```

### Usage — localized toolbar labels

```razor
<RichTextEditor @bind-Value="Model.Body"
                BoldLabel="@Localizer["Bold"]"
                ItalicLabel="@Localizer["Italic"]"
                InsertLinkPromptText="@Localizer["InsertLinkPrompt"]" />
```

### Usage — HtmlField convenience in a BaseCard

The quickest way to wire a rich-text editor + sanitized display into a `BaseCard` field:

```csharp
new BaseCardBuilder<Article>()
    .Field(a => a.Body, f => f
        .Label("Body")
        .ColSpan(2)
        .HtmlField())
    .Build();
```

`HtmlField()` sets `EditorTemplate` to a `RichTextEditor` two-way-bound to the field value and `DisplayTemplate` to a `SanitizedHtml` component. It is equivalent to wiring them manually (see below).

### Usage — EditorTemplate / DisplayTemplate manually

```csharp
new BaseCardBuilder<Article>()
    .Field(a => a.Body, f => f
        .Label("Body")
        .ColSpan(2)
        .EditorTemplate(ctx => builder =>
        {
            builder.OpenComponent<RichTextEditor>(0);
            builder.AddAttribute(1, nameof(RichTextEditor.Value), ctx.Value as string);
            builder.AddAttribute(2, nameof(RichTextEditor.ValueChanged),
                EventCallback.Factory.Create<string?>(this, v => ctx.ValueChanged.InvokeAsync(v)));
            builder.CloseComponent();
        })
        .DisplayTemplate(ctx => builder =>
        {
            builder.OpenComponent<SanitizedHtml>(0);
            builder.AddAttribute(1, nameof(SanitizedHtml.Value), ctx.Value as string);
            builder.CloseComponent();
        }))
    .Build();
```

---

## SanitizedHtml Component

`BlazorBase.CRUD.Components.SanitizedHtml.SanitizedHtml` renders an HTML string safely inside a `<div class="sanitized-html">` root element.

### Parameters

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `Value` | `string?` | `null` | The HTML to display. |

### Behavior

- When `IHtmlSanitizer` is registered in DI, the value is sanitized before being rendered as a `MarkupString`.
- When no `IHtmlSanitizer` is registered, the value is HTML-encoded and rendered as plain text — **never as raw markup** — so the component is XSS-safe by default even without a sanitizer.

### Usage

```razor
<SanitizedHtml Value="@storedHtml" />
```

---

## FieldBuilder EditorTemplate / DisplayTemplate / HtmlField

`FieldBuilder<TModel>` (in `BaseCardBuilder.cs`) exposes fluent setters for custom field rendering inside `BaseCard`:

```csharp
FieldBuilder<TModel> EditorTemplate(RenderFragment<PropertyFieldContext<TModel>> template)
FieldBuilder<TModel> DisplayTemplate(RenderFragment<PropertyFieldContext<TModel>> template)
FieldBuilder<TModel> HtmlField()
```

`HtmlField()` is a convenience that sets both templates to `RichTextEditor` (editor) and `SanitizedHtml` (display).

`PropertyFieldContext<TModel>` carries:

| Property | Type | Notes |
|---|---|---|
| `Model` | `TModel` | The bound entity. |
| `Value` | `object?` | The current property value. |
| `IsEditing` | `bool` | `true` when the field is in edit mode. |
| `ValueChanged` | `EventCallback<object?>` | Invoke to push a new value back to the model. |

`BasePropertyInput` renders `EditorTemplate` when `IsEditing` is true and `DisplayTemplate` when it is false, before falling back to the default typed inputs. Setting only one of the two templates is valid — the other falls back to the default rendering for that mode.

---

## IHtmlSanitizer

`BlazorBase.CRUD.Sanitization.IHtmlSanitizer` is a generic abstraction for HTML sanitization. It is **interface-only** — no implementation is provided here.

```csharp
namespace BlazorBase.CRUD.Sanitization;

public interface IHtmlSanitizer
{
    string Sanitize(string? html);
}
```

- Returns a non-null `string`; returns `""` for `null` input.
- The concrete implementation belongs in the consuming application (register as singleton).
- `SanitizedHtml` resolves `IHtmlSanitizer` optionally from DI; when absent it falls back to HTML-encoding (XSS-safe plain text).
- A `Ganss.Xss`-backed implementation is the recommended choice for server-side use:

```csharp
public class HtmlSanitizerAdapter(Ganss.Xss.HtmlSanitizer sanitizer) : IHtmlSanitizer
{
    public string Sanitize(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;
        return sanitizer.Sanitize(html);
    }
}
```

Register in the consuming server project:
```csharp
builder.Services.AddSingleton<IHtmlSanitizer, HtmlSanitizerAdapter>();
```

No `Ganss.Xss` or any other sanitizer package is referenced by `BlazorBase.CRUD` — keeping the library and all WASM/MAUI clients free of that dependency.
