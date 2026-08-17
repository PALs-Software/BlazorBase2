# BlazorBase.CRUD

A reusable Razor Class Library for type-safe CRUD operations with [FluentUI](https://www.fluentui-blazor.net/).  
Provides ready-made UI components (list, card, dialogs), an abstracted data layer and Minimal API endpoints — all generic and extensible.

---

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Setup & Registration](#setup--registration)
- [Render Modes: Server, WebAssembly & MAUI](#render-modes-server-webassembly--maui)
- [Defining an Entity](#defining-an-entity)
- [Auto-Registration via Assembly Scanning](#auto-registration-via-assembly-scanning)
- [Manual Registration](#manual-registration)
- [Registering Server Endpoints](#registering-server-endpoints)
- [BaseList — List View](#baselist--list-view)
- [BaseCard — Detail View / Form](#basecard--detail-view--form)
- [Configuration via Fluent Builder](#configuration-via-fluent-builder)
- [Custom Actions](#custom-actions)
- [Navigation Properties](#navigation-properties)
- [Field Security](#field-security)
- [Optimistic Concurrency](#optimistic-concurrency)
- [Dynamic Field Selection](#dynamic-field-selection)
- [Fluent Query Builder](#fluent-query-builder)
- [Localization](#localization)
- [Interceptors — Lifecycle Hooks](#interceptors--lifecycle-hooks)
- [Validation](#validation)
- [Audit Fields](#audit-fields)
- [Source Generator (DTO)](#source-generator-dto)
- [API Reference](#api-reference)
- [RichTextEditor Component](#richtexteditor-component)
- [DiffViewer Component](#diffviewer-component)
- [FileTree Component](#filetree-component)
- [Diff Models (BlazorBase.CRUD.Models.Diff)](#diff-models-blazorbasecrudmodelsdiff)
- [FileTree Models (BlazorBase.CRUD.Models.FileTree)](#filetree-models-blazorbasecrudmodelsfiletree)
- [IHtmlSanitizer](#ihtmlsanitizer)
- [BaseValidationException](#basevalidationexception)

---

## Architecture Overview

```
┌────────────────────────────────────────────────────────┐
│  UI Components (BaseList, BaseCard, BaseListPart,      │
│                 BaseLookupDialog)                       │
│  ↕ Parameters / Events                                 │
├────────────────────────────────────────────────────────┤
│  IBaseDataProvider<TModel>                             │
│  ├─ DbContextBaseDataProvider  (Server / EF Core)      │
│  └─ HttpBaseDataProvider       (Client / REST)         │
├────────────────────────────────────────────────────────┤
│  Minimal API Endpoints  (Server project only)          │
│  POST query · GET {id} · GET count · POST · PATCH {id} │
│  DELETE {id} · Field Security · Concurrency 409        │
├────────────────────────────────────────────────────────┤
│  EF Core DbContext + BaseSaveChangesInterceptor        │
│  [BaseCrud] Assembly Scan · [CrudAccess(roles, RIMD)]  │
└────────────────────────────────────────────────────────┘
```

All UI components work exclusively against `IBaseDataProvider<TModel>`.  
The appropriate implementation is registered via DI depending on the render mode:

| Render Mode | DataProvider | Direct DB Access? |
|---|---|---|
| **Server (SSR / Interactive Server)** | `DbContextBaseDataProvider` | Yes — EF Core |
| **WebAssembly (WASM)** | `HttpBaseDataProvider` | No — REST via `HttpClient` |
| **MAUI / Hybrid** | `HttpBaseDataProvider` | No — REST via `HttpClient` |

---

## Setup & Registration

### Project Reference

```xml
<ProjectReference Include="..\Libs\BlazorBase.CRUD\BlazorBase.CRUD.csproj" />
```

### Basic Setup (all projects using BlazorBase.CRUD)

```csharp
builder.Services.AddBlazorBaseCrud();
```

### With Audit User Provider

```csharp
builder.Services.AddBlazorBaseCrud<MyAuditUserProvider>();
```

---

## Render Modes: Server, WebAssembly & MAUI

### Server Project

The server project has direct database access via EF Core.  
It uses `DbContextBaseDataProvider` and registers the REST endpoints consumed by client projects.

**Important:**
- `AddBaseDbContext<TContext>(...)` registers the `DbContext` as a **factory** (`IDbContextFactory`), not as scoped.
- Each DataProvider creates and disposes its own `DbContext` per operation — this is **thread-safe** and compatible with Blazor Server's long-lived circuits.
- The `BaseSaveChangesInterceptor` is automatically injected into the factory and populates audit fields on every `SaveChanges`.

```csharp
// Program.cs (Server)

// 1. Register DbContext (factory pattern)
builder.Services.AddBaseDbContext<MyDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Assembly scan — registers DbContextBaseDataProvider for every [BaseCrud] entity
builder.Services.AddBlazorBaseCrudServer<MyDbContext>();

// 3. Assembly scan — maps CRUD endpoints for every [BaseCrud] entity
app.MapBlazorBaseCrudEndpoints();
```

> **Note:** In a pure server-rendered app, steps 1 and 2 are sufficient. Endpoints (step 3) are only required when WebAssembly or MAUI clients need access to the same data.

### WebAssembly Project

WebAssembly runs in the browser and has **no** access to the database.  
All data is fetched from the server via REST endpoints.

```csharp
// Program.cs (WebAssembly)

builder.Services.AddBlazorBaseCrud();

// Interactive UI services the CRUD components depend on (IConfirmationService) —
// call after AddFluentUIComponents()
builder.Services.AddBlazorBaseCrudComponents();

// Assembly scan — registers HttpBaseDataProvider for every [BaseCrud] entity
builder.Services.AddBlazorBaseCrudClient(client =>
{
    client.BaseAddress = new Uri("https://my-api.example.com/api/base/");
});
```

**Important:**
- The `HttpClient` base address should point to the API route prefix (default `api/base/`). The route from `[BaseCrud("products")]` is appended automatically.
- Authentication is typically handled via a `DelegatingHandler` that attaches the auth token.
- `HttpBaseDataProvider` communicates over 6 endpoints: `POST .../query`, `GET .../{id}`, `GET .../count`, `POST ...`, `PATCH .../{id}`, `DELETE .../{id}`.

### MAUI / Hybrid Project

MAUI apps also use `HttpBaseDataProvider` and communicate with the server over REST.

```csharp
// MauiProgram.cs

builder.Services.AddBlazorBaseCrud();

// Interactive UI services the CRUD components depend on (IConfirmationService) —
// call after AddFluentUIComponents()
builder.Services.AddBlazorBaseCrudComponents();

builder.Services.AddBlazorBaseCrudClient(client =>
{
    client.BaseAddress = new Uri("https://my-api.example.com/api/base/");
});
```

### Summary: What Gets Registered Where

| Registration | Server | WASM | MAUI |
|---|:---:|:---:|:---:|
| `AddBlazorBaseCrud()` | ✓ | ✓ | ✓ |
| `AddBlazorBaseCrudComponents()` | — | ✓ | ✓ |
| `AddBaseDbContext<T>(...)` | ✓ | — | — |
| `AddBlazorBaseCrudServer<T>()` | ✓ | — | — |
| `AddBlazorBaseCrudClient(...)` | — | ✓ | ✓ |
| `MapBlazorBaseCrudEndpoints()` | If serving WASM/MAUI | — | — |
| `AddBaseDataInterceptor<T, I>(...)` | ✓ | Optional | Optional |
| `AddBaseValidator<T, V>(...)` | ✓ | ✓ | ✓ |

---

## Defining an Entity

Entities are C# classes decorated with `[BaseCrud]` for auto-registration. They can optionally inherit from `AuditModel`.

```csharp
[BaseCrud("products")]
public class Product
{
    public Guid Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string? Description { get; set; }

    // Reference navigation — auto-detected, renders as select/browse
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    // Fields excluded from all CRUD operations (no roles get any rights)
    [CrudAccess("*", "")]
    public string InternalNotes { get; set; } = string.Empty;
}

// With audit fields + concurrency
[BaseCrud("orders")]
public class Order : AuditModel
{
    public Guid Id { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public DateTime OrderDate { get; set; }

    // Collection navigation — rendered as ListPart in BaseCard
    public ICollection<OrderItem> Items { get; set; } = [];

    // CreatedOn, CreatedBy, ModifiedOn, ModifiedBy come from AuditModel
    // ModifiedOn has [ConcurrencyCheck] for optimistic concurrency
}
```

### The `[BaseCrud]` Attribute

```csharp
[BaseCrud("products")]  // Route used for endpoints and HttpClient registration
public class Product { ... }
```

Marks the entity for assembly scanning. Both `AddBlazorBaseCrudServer<TContext>()` and `AddBlazorBaseCrudClient(...)` scan for this attribute and auto-register the appropriate `IBaseDataProvider<T>`. `MapBlazorBaseCrudEndpoints()` also uses it to map REST endpoints.

---

## Auto-Registration via Assembly Scanning

The preferred way to register entities. All classes with `[BaseCrud]` are discovered and registered automatically.

### Server

```csharp
// Registers DbContextBaseDataProvider<T> for each [BaseCrud] entity
builder.Services.AddBlazorBaseCrudServer<MyDbContext>();

// Maps CRUD endpoints for each [BaseCrud] entity
app.MapBlazorBaseCrudEndpoints();
```

### Client (WASM / MAUI)

```csharp
// Registers the interactive UI services the CRUD components depend on
// (IConfirmationService) — call after AddFluentUIComponents()
builder.Services.AddBlazorBaseCrudComponents();

// Registers HttpBaseDataProvider<T> for each [BaseCrud] entity
builder.Services.AddBlazorBaseCrudClient(client =>
{
    client.BaseAddress = new Uri("https://my-api.example.com/api/base/");
});
```

Both methods accept an optional `Assembly` parameter to scan a specific assembly:

```csharp
builder.Services.AddBlazorBaseCrudServer<MyDbContext>(typeof(Product).Assembly);
```

---

## Manual Registration

For entities without `[BaseCrud]` or when custom configuration is needed:

### Server-Side: EF Core

Use `AddBaseDbContext<TContext>()` together with individual service registrations if you need custom `queryCustomizer` or other overrides not covered by assembly scanning.

### Client-Side: HTTP

```csharp
// Option 1: Direct URL
builder.Services.AddBaseHttpDataProvider<Product>("https://my-api.example.com/api/base/products");

// Option 2: Configure HttpClient
builder.Services.AddBaseHttpDataProvider<Product>(client =>
{
    client.BaseAddress = new Uri("https://my-api.example.com/api/base/products");
});
```

---

## Registering Server Endpoints

### Auto (Preferred)

```csharp
// Scans for [BaseCrud] and maps all endpoints
app.MapBlazorBaseCrudEndpoints();
```

### Manual (Per Entity)

```csharp
app.MapBlazorBaseCrudEndpoints<Product>("products");
// → POST   /api/base/products/query
// → GET    /api/base/products/{id}
// → GET    /api/base/products/count
// → POST   /api/base/products
// → PATCH  /api/base/products/{id}
// → DELETE /api/base/products/{id}
```

### With Configuration

```csharp
app.MapBlazorBaseCrudEndpoints<Product>("products", options =>
{
    options.RoutePrefix = "api/v2";           // Default: "api/base"
    options.RequireAuth = true;                // Default: true
    options.AuthorizationPolicy = "AdminOnly"; // Optional: named policy
    options.UserFilter = user => p => p.OwnerId == user.FindFirst(ClaimTypes.NameIdentifier)!.Value;
});
```

ID parameters are automatically parsed as `string`, `Guid`, `int`, or `long`.

`UserFilter` (`Func<ClaimsPrincipal, Expression<Func<TEntity, bool>>>?`) is a per-request row predicate enforced as a row-level-security boundary: it is passed through as `scopeFilter` on `query`/`count`/`GET {id}`, and — via a scoped existence pre-check — makes `PATCH`/`DELETE` return **404** for rows outside the caller's scope, instead of leaking whether the row exists.

> **BREAKING:** `BaseEndpointOptions.QueryCustomizer` has been removed (it was non-functional/dead code). Use the server provider's DI-level `queryCustomizer` constructor argument (see [Manual Registration](#manual-registration)) for query shaping, and `UserFilter` above for per-user row scoping.

---

## BaseList — List View

### Option 1: Declarative (Child Components)

```html
@* ProductList.razor *@

<BaseList TModel="Product" AllowAdd="true">
    <BaseColumn TModel="Product"
                Property="@(x => x.Name)"
                Title="Product Name" />

    <BaseColumn TModel="Product"
                Property="@(x => x.Price)"
                Title="Price"
                Format="C2"
                Width="120px" />
</BaseList>
```

> `DataProvider` is optional — if omitted, `IBaseDataProvider<TModel>` is resolved from DI automatically.  
> All columns are **sortable** and **filterable** by default. Opt out per column with `Sortable="false"` / `Filterable="false"`.

`Width` is any valid CSS grid track (`120px`, `10%`, `0.5fr`). Columns without one share the remaining
space equally, but never shrink below what their content needs — on a narrow viewport the grid scrolls
sideways instead of truncating every column to an ellipsis, because an ellipsis hides the value and
scrolling does not. A column holding long prose would grow far too wide for that rule: give it an
explicit `Width`, or set `--base-list-column-min-width` (default `max-content`) to a fixed length on
the list or any ancestor.

A `bool` column needs no `Format`: it renders the framework's localized `BoolTrue`/`BoolFalse`
("Yes"/"No"), the same wording the filter panel offers for that property.

### Option 2: Fluent Builder (Code-Based)

```csharp
// ProductList.razor.cs
public partial class ProductList : ComponentBase
{
    private BaseListConfiguration<Product> ListConfig { get; } =
        new BaseListBuilder<Product>()
            .Column(x => x.Name, c => c.Title("Product Name").Sortable().Filterable())
            .Column(x => x.Price, c => c.Title("Price").Format("C2").Sortable().Width("120px"))
            .Build();
}
```

```html
<BaseList TModel="Product" Configuration="@ListConfig" AllowAdd="true" />
```

### Connecting List & Card (Edit Dialog)

When `AllowEdit="true"` (default), clicking anywhere on a row opens a `BaseDialog<TModel>` containing a `BaseCard`. Pass your card configuration via the `CardConfiguration` parameter:

```csharp
// ProductList.razor.cs
public partial class ProductList : ComponentBase
{
    private BaseListConfiguration<Product> ListConfig { get; } =
        new BaseListBuilder<Product>()
            .Column(x => x.Name, c => c.Title("Product Name").Sortable().Filterable())
            .Column(x => x.Price, c => c.Title("Price").Format("C2").Sortable().Width("120px"))
            .Build();

    private BaseCardConfiguration<Product> CardConfig { get; } =
        new BaseCardBuilder<Product>()
            .Field(x => x.Name, f => f.Label("Product Name").Required())
            .Field(x => x.Price, f => f.Label("Price").Required())
            .Field(x => x.Description, f => f.Label("Description").Lines(4).ColSpan(2))
            .Build();
}
```

```html
<BaseList TModel="Product" Configuration="@ListConfig" CardConfiguration="@CardConfig" AllowAdd="true" AllowEdit="true" />
```

### Dynamic Field Selection

`BaseList` automatically builds a `Select` list from the configured column property names + `Id`. Only the required fields are requested from the server, reducing payload size.

### Features

- **Row Click to Edit**: Click anywhere on a row to open the edit dialog (when `AllowEdit="true"`).
- **Hover Effect**: Rows highlight on mouse-over with a pointer cursor for clear affordance.
- **Context Menu**: Right-click any row to access Edit, Delete, Select/Deselect, and custom actions.
- **Multi-Select**: Hold **Shift+Click** to select/deselect rows, or use the context menu. A round selection indicator column appears automatically when the first row is selected.
- **Bulk Delete**: When multiple rows are selected, the context menu "Delete" action applies to all selected rows. A confirmation dialog is shown before deletion.
- **Custom Context Menu Actions**: Use the `ContextMenuActions` template to add custom menu items per row (rendered as `<div role="menuitem">` elements).
- **Infinite Scroll**: The list automatically loads more items when the user reaches the end.
- **Filtering**: Every column is filterable by default. Disable per column with `Filterable="false"`.
- **Sorting**: Every column is sortable by default. Disable per column with `Sortable="false"`.
- **Add/Edit/Delete**: Controllable via `AllowAdd`, `AllowEdit`, `AllowDelete`.
- **Role-Based Access**: Use `[CrudAccess]` on entities/properties or `.Access(roles, rights)` on columns to restrict R/I/M/D rights by role.
- **Auto DataProvider**: If `DataProvider` parameter is omitted, resolves `IBaseDataProvider<TModel>` from DI.
- **Item Deep Linking**: The open card is reflected in the URL as a query parameter, so items are directly linkable and shareable (on by default).

### Item Deep Linking

The open card is bound to the URL out of the box — **no per-page code**. Opening an item (row click,
context-menu edit, or navigating to a deep link) adds `?item=<id>` to the current list URL; closing the card
removes it; the browser **Back** button closes an open card. This makes any item directly linkable and shareable
and works on every existing list route.

- `EnableDeepLinking` (`bool`, default `true`) — set `false` to opt a list out (e.g. an embedded/secondary list).
- `DeepLinkParameterName` (`string`, default `"item"`) — the query-parameter name. Override it when more than one
  deep-linked list renders on the same page, to avoid a collision.

The URL is the single source of truth for which item is open: a click navigates (adds the parameter) and one
handler then loads the item via `GetByIdAsync` and opens the card, so access rights (server-side field stripping,
the "open only when editable" gate) are enforced exactly as for a normal edit. A parameter that names an unknown
or inaccessible id opens nothing and is stripped from the URL. The id in the URL is converted to the entity's `Id`
property type, so `Guid`, `int`, `long` and `string` keys all work.

```html
<!-- Deep-linkable by default: /products?item=<id> opens that product's card -->
<BaseList TModel="Product" CardType="typeof(ProductCard)" />

<!-- Opt out, or rename the parameter for a second list on the same page -->
<BaseList TModel="Product" EnableDeepLinking="false" />
<BaseList TModel="Order" DeepLinkParameterName="order" />
```

### Context Menu Actions

Use the `ContextMenuActions` render fragment to add custom items to the right-click context menu:

```html
<BaseList TModel="Product" AllowAdd="true">
    <ChildContent>
        <BaseColumn TModel="Product" Property="@(x => x.Name)" Title="Product Name" />
        <BaseColumn TModel="Product" Property="@(x => x.Price)" Title="Price" Format="C2" />
    </ChildContent>
    <ContextMenuActions>
        <div role="menuitem" @onclick="() => DoSomething(context)">
            <FluentIcon Value="@(new Icons.Regular.Size16.Star())" />
            <span>Custom Action</span>
        </div>
    </ContextMenuActions>
</BaseList>
```

### Events

| Event | Type | Description |
|---|---|---|
| `OnBeforeItemCreate` | `EventCallback<ItemEventArgs<TModel>>` | Before creation — can be cancelled |
| `OnAfterItemCreated` | `EventCallback<TModel>` | After creation |
| `OnBeforeItemUpdate` | `EventCallback<ItemEventArgs<TModel>>` | Before update — can be cancelled |
| `OnAfterItemUpdated` | `EventCallback<TModel>` | After update |
| `OnBeforeItemDelete` | `EventCallback<ItemEventArgs<TModel>>` | Before deletion — can be cancelled |
| `OnAfterItemDeleted` | `EventCallback<TModel>` | After deletion |
| `OnItemSelected` | `EventCallback<TModel>` | Row clicked (fires before edit dialog opens) |

---

## BaseCard — Detail View / Form

### Option 1: Declarative

```html
<BaseCard TModel="Product" Model="@SelectedProduct" MaxColumns="2">
    <PropertyField TModel="Product"
                   Property="@(x => x.Name)"
                   Label="Product Name"
                   Required="true"
                   Placeholder="e.g. Widget Pro" />

    <PropertyField TModel="Product"
                   Property="@(x => x.Price)"
                   Label="Price"
                   Required="true" />

    <PropertyField TModel="Product"
                   Property="@(x => x.Description)"
                   Label="Description"
                   Lines="4"
                   ColSpan="2" />
</BaseCard>
```

> `DataProvider` is optional — if omitted, `IBaseDataProvider<TModel>` is resolved from DI automatically.

### Option 2: Fluent Builder

```csharp
private BaseCardConfiguration<Product> CardConfig { get; } =
    new BaseCardBuilder<Product>()
        .Field(x => x.Name, f => f.Label("Product Name").Required().Placeholder("e.g. Widget Pro"))
        .Field(x => x.Price, f => f.Label("Price").Required())
        .Field(x => x.Description, f => f.Label("Description").Lines(4).ColSpan(2))
        .Build();
```

```html
<BaseCard TModel="Product" Model="@SelectedProduct" Configuration="@CardConfig" MaxColumns="2" OnAfterSave="@OnSaved" />
```

### Dirty Field Tracking

`BaseCard` automatically tracks which fields have been modified. On save:

1. A snapshot of the original values is taken when the model is loaded.
2. On submit, a diff is computed: only changed fields are sent via `PatchAsync`.
3. For entities with `AuditModel`, a concurrency stamp (`ModifiedOn`) is included.
4. If a concurrency conflict occurs, a user-friendly error message is displayed.

### Confirm on Close with Unsaved Changes

When a `BaseCard` is hosted in a dialog (the default when `BaseList`/`BaseListPart` open it),
every user close path — the Cancel button, the card's own close (X) button, the `Escape` key,
and an overlay click — runs through one pipeline:

1. If the snapshot diff is empty **and** no list part reports pending changes, the card closes.
2. Otherwise a confirmation ("discard changes?") is shown. Confirming reverts the model to its
   snapshot (so the discarded edits no longer leak into the list, which holds the same object
   reference) and closes; declining keeps the card open.

The dialog's native FluentUI dismiss is disabled (`PreventDismissOnOverlayClick = true`,
`ShowDismiss = false`); `Escape`/overlay clicks are intercepted by the
`_content/BlazorBase.CRUD/js/baseCardDismiss.js` interop shim and routed through the same pipeline,
so declining can keep the dialog open (FluentUI 4.x cannot veto a native dismiss). The confirmation
is shown through the injectable `IConfirmationService` (default `FluentUiConfirmationService`,
registered by `AddBlazorBaseCrudComponents()`); substitute it — by registering your own
`IConfirmationService` before that call — to customize or to stub it in tests.

### Card Header Title (Display Key)

The `BaseCard` header shows a **display key** so it is clear which record is being edited.
By default this is the primary key (`Id`); for a GUID or other key that reads poorly for humans,
mark one or more descriptive properties as display keys instead. Multiple keys are combined in
ascending order, joined by a separator (default `" · "`), and null/empty/default values are skipped.
A new (unsaved) record falls back to the "Add"/"Edit" caption.

Resolution precedence is **most specific wins, no merge**: card-level configuration (fluent or
markup) → `[DisplayKey]` on the entity → primary key.

```csharp
// Attribute — entity-wide, lowest precedence
public class Project
{
    public Guid Id { get; set; }

    [DisplayKey(Order = 1)]
    public string Name { get; set; } = "";

    [DisplayKey(Order = 2)]
    public string Status { get; set; } = "";   // header: "Acme Portal · Active"
}
```

```html
<!-- Markup — dedicated marker (need not be an editable field) … -->
<BaseCard TModel="Project" Model="@Model">
    <DisplayKeyField Property="@(x => x.Name)" Order="1" />
    <PropertyField Property="@(x => x.Name)" />
    <!-- … or a flag on an existing field -->
    <PropertyField Property="@(x => x.Status)" IsDisplayKey DisplayKeyOrder="2" />
</BaseCard>
```

```csharp
// Fluent — dedicated method or a flag on a field; optional custom separator
new BaseCardBuilder<Project>()
    .DisplayKey(x => x.Name)
    .Field(x => x.Status, f => f.AsDisplayKey(order: 1))
    .DisplayKeySeparator(" — ")
    .Build();
```

The card renders the title in its own header (`.base-card-header`); the hosting dialog's native
FluentUI title is suppressed (`ShowTitle = false`) so there is a single header. The same display-key
resolution drives the FK lookup display text — see [BaseLookupDialog](#baselookupdialog).

### Automatic Field Detection

`BasePropertyInput` automatically renders the appropriate FluentUI control based on the property type:

| C# Type | Rendered Control |
|---|---|
| `string` | `FluentTextField` |
| `string` (with `Lines > 1`) | `FluentTextArea` |
| `bool` / `bool?` | `FluentSwitch` |
| `DateTime` / `DateTimeOffset` | `FluentDatePicker` |
| `int`, `long`, `decimal`, `double`, `float` (+ nullable) | `FluentTextField` (numeric) |
| `enum` | `FluentSelect` with all enum values |
| Reference navigation (≤ threshold) | `FluentSelect` with all items |
| Reference navigation (> threshold) | Text field + Browse button |
| Custom Template | User-provided `EditorTemplate` / `DisplayTemplate` |

### Field Groups

Fields can be grouped into accordion sections using `Group`:

```csharp
new BaseCardBuilder<Order>()
    .Group("General", g => g
        .Field(x => x.CustomerName, f => f.Label("Customer"))
        .Field(x => x.OrderDate, f => f.Label("Order Date"))
    )
    .Group("Details", g => g
        .Field(x => x.Notes, f => f.Label("Notes").Lines(3))
    )
    .Build();
```

### ListParts — Embedded Child Collections

```html
<BaseCard TModel="Order" Model="@SelectedOrder">
    <PropertyField TModel="Order" Property="@(x => x.CustomerName)" Label="Customer" />

    <ListPartField TModel="Order"
                   Property="@(x => x.Items)"
                   AllowAdd="true"
                   AllowDelete="true" />
</BaseCard>
```

A list part edits each child item in a per-item dialog. By default that dialog builds a
`BaseCard` from the child configuration, but you can supply your own card component via
`ChildCardType` — the markup analog of `CardType` on `BaseList`. This lets you keep list,
card and sub-card in separate component files:

```html
<ListPartField TModel="Order"
               Property="@(x => x.Items)"
               AllowAdd="true"
               AllowDelete="true"
               ChildCardType="typeof(OrderItemCard)" />
```

The `ChildCardType` component is hosted in **defer-save mode** (changes are flushed together
with the parent on save), so it must accept `Model`, `Localizer`, `DeferSave`, `OnDeferredSave`
and `OnCancelled` parameters and forward them to an inner `BaseCard` — typically just wrapping
its own `PropertyField` markup.

That list is complete: a wrapper card never has to forward whether the item is new. Because a
custom card type is instantiated through `DynamicComponent`, it would only receive parameters it
declares itself, so the hosting dialog cascades that answer (`CardCascadeNames.IsNew`) straight to
the inner `BaseCard` instead. The same holds for `CardType` on `BaseList`. Without it the inner card
falls back to guessing from the key, which reads an entity that **assigns its key before saving**
(a sequential GUID, say) as an existing row — captioning the header "edit", checking modify instead
of insert rights, and sending a creation down the update path.

### Custom Templates

```html
<PropertyField TModel="Product" Property="@(x => x.Color)" Label="Color">
    <EditorTemplate>
        <FluentSelect @bind-Value="@context.Value"
                       Items="@Colors"
                       OptionText="@(c => c.Name)" />
    </EditorTemplate>
</PropertyField>
```

---

## Registrable Custom Property Components

The per-field `EditorTemplate` / `DisplayTemplate` is a one-off escape hatch. When you want a
**reusable** control that the generic card and list pick up automatically for *every* consumer (by
property type, a custom attribute, or property name), register a custom property component. This is
fully generic and **WASM-safe** — components receive their data via parameters and never touch a
`DbContext`.

### Interfaces

| Interface | Purpose |
|---|---|
| `IBaseCustomPropertyInput` | A card edit/detail control. `bool CanHandle(CustomPropertyContext)` opts in; `[Parameter]`s: `object Model`, `PropertyInfo Property`, `object? Value`, `EventCallback<object?> ValueChanged`, `bool IsEditing`, `bool ReadOnly`, `IStringLocalizer? Localizer`. |
| `IBaseCustomPropertyDisplay` | A list-cell display. `CanHandle` opts in (`IsEditing` is always `false`); `[Parameter]`s: `object Model`, `PropertyInfo Property`, `object? Value`, `IStringLocalizer? Localizer`. |
| `ICardSaveParticipant` | Optional. A custom input implements this to take part in the save lifecycle: `Task<bool> ValidateAsync()` (return `false` to block the save), `Task OnBeforeSaveAsync(CardSaveContext)`, `Task OnAfterSaveAsync(CardSaveContext)`. |
| `CustomPropertyContext` | `record (Type ModelType, PropertyInfo Property, Type PropertyType /* nullable unwrapped */, bool IsEditing)` — the decision input for `CanHandle`. Keep `CanHandle` synchronous, pure and cheap. |
| `CardSaveContext` | Carries `object Model`, `bool IsCreate`, and `AddMessage(string)` / `Message` to surface a message back to the card. |

### Registration

```csharp
builder.Services.AddBlazorBaseCustomInput<AccessTokenSecretInput>();
builder.Services.AddBlazorBaseCustomDisplay<StatusBadgeDisplay>();
```

Both are scoped. Multiple registrations are allowed; the **first** registered component whose
`CanHandle` returns `true` (registration order) wins. The resolution decision is cached per
`(model type, property, edit mode)`, so the registered set is iterated once per field shape, not per
render.

### Render precedence

For a card field, `BasePropertyInput` chooses, in order:

1. the per-field `EditorTemplate` / `DisplayTemplate` (explicit per-field override — **always wins**),
2. else the first registered `IBaseCustomPropertyInput` whose `CanHandle` returns `true`,
3. else the built-in type-based chain (bool / date / numeric / enum / navigation / textarea / text).

For a list cell, `BaseList` chooses: the column `Template` first, else the first matching
`IBaseCustomPropertyDisplay`, else the default cell text. Resolution is purely additive — nothing
changes for existing consumers unless a component is registered *and* its `CanHandle` matches.

### Worked example — a custom input with a save participant

A control that renders a secret field and reveals the freshly generated value exactly once after the
record is saved:

```csharp
public sealed class AccessTokenSecretInput : ComponentBase, IBaseCustomPropertyInput, ICardSaveParticipant
{
    [Parameter] public object Model { get; set; } = default!;
    [Parameter] public PropertyInfo Property { get; set; } = default!;
    [Parameter] public object? Value { get; set; }
    [Parameter] public EventCallback<object?> ValueChanged { get; set; }
    [Parameter] public bool IsEditing { get; set; }
    [Parameter] public bool ReadOnly { get; set; }
    [Parameter] public IStringLocalizer? Localizer { get; set; }

    public bool CanHandle(CustomPropertyContext context) =>
        context.Property.GetCustomAttribute<SecretAttribute>() is not null;

    public Task<bool> ValidateAsync() => Task.FromResult(true);

    public Task OnBeforeSaveAsync(CardSaveContext context) => Task.CompletedTask;

    public Task OnAfterSaveAsync(CardSaveContext context)
    {
        var generated = (string?)Property.GetValue(context.Model);
        context.AddMessage($"Secret (shown once): {generated}");
        StateHasChanged();
        return Task.CompletedTask;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) { /* render the control */ }
}
```

```csharp
builder.Services.AddBlazorBaseCustomInput<AccessTokenSecretInput>();
```

During `BaseCard` save the lifecycle runs: `ValidateAsync` on every collected participant (a `false`
aborts the save), then `OnBeforeSaveAsync`, then the persist via `IBaseDataProvider`, then
`OnAfterSaveAsync`. A custom input that does **not** need save-time behavior simply omits
`ICardSaveParticipant`.

---

## Configuration via Fluent Builder

Both approaches (declarative and builder) can be mixed — declarative child components are collected via a collector pattern. When a `Configuration` parameter is set, it takes precedence.

### BaseListBuilder

```csharp
new BaseListBuilder<TModel>()
    .Column(x => x.Property, c => c
        .Title("Title")
        .Sortable()
        .Filterable()
        .Visible()
        .Format("N2")
        .Width("200px")
    )
    .Build();
```

### BaseCardBuilder

```csharp
new BaseCardBuilder<TModel>()
    .Field(x => x.Property, f => f
        .Label("Label")
        .Editable()
        .Required()
        .Placeholder("...")
        .ColSpan(2)
        .Lines(3)
        .LookupThreshold(50)           // Navigation: override the 20-item threshold
        .DisplayProperty("Name")       // Navigation: which property to display
    )
    .Group("Group Name", g => g
        .Field(x => x.Prop1, f => f.Label("..."))
        .Field(x => x.Prop2, f => f.Label("..."))
    )
    .ListPart<ChildType>(x => x.Children, lp => lp
        .AllowAdd()
        .AllowDelete()
        .AllowReorder()
        .ComponentType<CustomChildComponent>()  // Custom render component (replaces the list part)
        .ChildCardType<OrderItemCard>()         // Custom per-item edit card (defer-save)
        .ChildCardConfig(cardConfig)             // Inline card editing
        .ChildListConfig(listConfig)             // Tabular child display
    )
    .Build();
```

---

## Custom Actions

Custom actions let you define buttons and menu items that appear in list toolbars, context menus, card headers, and list-part headers. Actions are organized in **named groups** and support dynamic visibility, role-based access, bulk operations, and dynamic component rendering.

### Where Actions Appear

Actions specify where they are shown via `CrudActionContext` (flags enum):

| Context | Location | Description |
|---|---|---|
| `ListToolbar` | Header area above the list (next to the Add button) | Toolbar buttons / dropdown menus |
| `ContextMenu` | Right-click context menu on list rows | Additional menu items grouped with dividers |
| `Card` | Above the form fields in BaseCard | Action buttons in the detail/edit view |
| `ListPart` | Header area of BaseListPart (next to the Add button) | Actions for embedded child collections |
| `All` | All of the above (default) | Shortcut for all contexts |

### Option 1: Fluent Builder API

Define action groups on `BaseListBuilder` or `BaseCardBuilder` using `.ActionGroup()`:

```csharp
private BaseListConfiguration<Product> ListConfig { get; } =
    new BaseListBuilder<Product>()
        .Column(x => x.Name, c => c.Title("Product Name"))
        .Column(x => x.Price, c => c.Title("Price").Format("C2"))
        .ActionGroup("Process", g => g
            .Caption("Actions")
            .Icon(new Icons.Regular.Size20.Play())
            .Action("Activate", a => a
                .Icon(new Icons.Regular.Size16.CheckmarkCircle())
                .ShowIn(CrudActionContext.ContextMenu | CrudActionContext.ListToolbar)
                .OnExecute(async args =>
                {
                    // args.Item — the single item (context menu) or null (toolbar)
                    // args.SelectedItems — all selected items (bulk actions)
                    // args.ServiceProvider — resolve services
                })
                .BulkAction())
            .Action("Export", a => a
                .Icon(new Icons.Regular.Size16.ArrowDownload())
                .ShowIn(CrudActionContext.ListToolbar)
                .OnExecute(async args => { /* export logic */ })))
        .Build();
```

Card-level actions:

```csharp
private BaseCardConfiguration<Product> CardConfig { get; } =
    new BaseCardBuilder<Product>()
        .Field(x => x.Name, f => f.Label("Product Name").Required())
        .Field(x => x.Price, f => f.Label("Price").Required())
        .ActionGroup("Related", g => g
            .Caption("Related")
            .ShowIn(CrudActionContext.Card)
            .Action("View Orders", a => a
                .Icon(new Icons.Regular.Size16.Cart())
                .OnExecute(async args => { /* navigate to orders */ })))
        .Build();
```

### Option 2: Declarative (Razor Markup)

Use `<BaseActionGroup>` and `<BaseAction>` as child components of `BaseList` or `BaseCard`:

```html
<BaseList TModel="Product">
    <ChildContent>
        <BaseColumn TModel="Product" Property="@(x => x.Name)" Title="Product Name" />
        <BaseColumn TModel="Product" Property="@(x => x.Price)" Title="Price" Format="C2" />
    </ChildContent>
    <ContextMenuActions>
        @* Legacy RenderFragment — still supported for backward compatibility *@
    </ContextMenuActions>

    <BaseActionGroup TModel="Product" Name="Process" Caption="Actions">
        <BaseAction TModel="Product"
                    Caption="Activate"
                    Icon="@(new Icons.Regular.Size16.CheckmarkCircle())"
                    Contexts="@(CrudActionContext.ContextMenu | CrudActionContext.ListToolbar)"
                    OnExecute="@(args => ActivateAsync(args.Item!))"
                    IsBulkAction="true" />
        <BaseAction TModel="Product"
                    Caption="Export"
                    Icon="@(new Icons.Regular.Size16.ArrowDownload())"
                    Contexts="CrudActionContext.ListToolbar"
                    OnExecute="@(args => ExportAsync())" />
    </BaseActionGroup>
</BaseList>
```

Both approaches can be mixed — declarative child components and builder configuration are merged.

### Rendering Behavior

- **Single-action groups** render as a direct button (no dropdown).
- **Multi-action groups** render as a `FluentMenuButton` dropdown with the group caption and icon.
- In context menus, each group is separated by a divider, and actions are listed as menu items.

### CrudActionBuilder Methods

| Method | Description |
|---|---|
| `.Caption(string)` | Display text (localized via parent's `IStringLocalizer`) |
| `.ToolTip(string)` | Tooltip text |
| `.Icon(Icon)` | Fluent UI icon |
| `.Appearance(Appearance)` | Button appearance (default: `Stealth`) |
| `.ShowIn(CrudActionContext)` | Where the action appears (default: `All`) |
| `.Order(int)` | Sort order within the group |
| `.Visible(Func<CrudActionVisibilityArgs<T>, Task<bool>>)` | Dynamic per-item visibility |
| `.VisibleForRoles(Func<ClaimsPrincipal, bool>)` | Role-based visibility |
| `.OnExecute(Func<CrudActionEventArgs<T>, Task>)` | Action callback |
| `.BulkAction(bool)` | Enable bulk mode — receives all selected items |
| `.DynamicComponent<TComponent>(params?, onClosed?)` | Render a component instead of executing a callback |

### CrudActionGroupBuilder Methods

| Method | Description |
|---|---|
| `.Caption(string)` | Group display text |
| `.Icon(Icon)` | Group icon |
| `.ShowIn(CrudActionContext)` | Where the group appears (default: `All`) |
| `.Order(int)` | Sort order among groups |
| `.Visible(...)` | Dynamic group visibility |
| `.VisibleForRoles(...)` | Role-based group visibility |
| `.Action(string, Action<CrudActionBuilder<T>>)` | Add an action to the group |

### CrudActionEventArgs

When an action is executed, it receives `CrudActionEventArgs<TModel>`:

| Property | Type | Description |
|---|---|---|
| `Item` | `TModel?` | The item the action was triggered on (null for toolbar-level) |
| `SelectedItems` | `IReadOnlyCollection<TModel>` | All selected items (for bulk actions) |
| `ServiceProvider` | `IServiceProvider` | Resolve any service from DI |
| `Context` | `CrudActionContext` | Where the action was triggered |

### Dynamic Visibility

Actions can be shown or hidden based on item state and/or user roles:

```csharp
.Action("Deactivate", a => a
    .Visible(async args => args.Item?.IsActive == true)
    .VisibleForRoles(user => user.IsInRole("Admin"))
    .OnExecute(async args => { /* ... */ }))
```

Visibility is re-evaluated on every Blazor render cycle — no explicit refresh needed.

### Bulk Actions

Actions marked with `.BulkAction()` receive all selected items in `args.SelectedItems`. In the toolbar, bulk action buttons are automatically disabled when no items are selected.

```csharp
.Action("Delete Selected", a => a
    .BulkAction()
    .ShowIn(CrudActionContext.ListToolbar)
    .OnExecute(async args =>
    {
        foreach (var item in args.SelectedItems)
            await DeleteItemAsync(item);
    }))
```

### Dynamic Component Rendering

Instead of a simple callback, an action can render a Blazor component (e.g. a wizard, dialog, or custom editor):

```csharp
.Action("Import", a => a
    .DynamicComponent<ImportWizard>(
        parameters: new() { ["MaxItems"] = 100 },
        onClosed: async args => await RefreshListAsync()))
```

The component should implement `ICrudActionComponent<TModel>`:

```csharp
public partial class ImportWizard : ComponentBase, ICrudActionComponent<Product>
{
    [Parameter]
    public CrudActionEventArgs<Product> Args { get; set; } = default!;

    [Parameter]
    public EventCallback OnClose { get; set; }

    private async Task FinishAsync()
    {
        // ... import logic ...
        await OnClose.InvokeAsync();
    }
}
```

### ActionGroups Parameter

You can also pass action groups directly as a parameter (e.g. when building them dynamically):

```html
<BaseList TModel="Product" ActionGroups="@MyActionGroups" />
```

### Backward Compatibility

The existing `ContextMenuActions` RenderFragment on `BaseList` is fully supported. If both structured action groups **and** the render fragment are provided, the render fragment items appear first, followed by the structured action groups (each separated by a divider).

---

## Navigation Properties

`BasePropertyInput` automatically detects reference and collection navigation properties.

### Reference Navigation (Many-to-1)

When a field's type is a class with an `Id` property, it is treated as a reference navigation:

```csharp
[BaseCrud("products")]
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // FK + navigation — auto-detected
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
}
```

**FK discovery order:**
1. `[ForeignKey("CategoryId")]` attribute on the navigation property
2. Convention: `{PropertyName}Id` (e.g., `Category` → `CategoryId`)
3. Convention: `{PropertyName}ID`

**UI behavior:**
- If the related table has **≤ 20 items**: renders a `FluentSelect` dropdown with all items loaded.
- If the related table has **> 20 items**: renders a read-only text field + "Browse..." button that opens an inline search/select panel.
- The threshold is configurable per field via `.LookupThreshold(50)`.
- Display text is resolved through the same **display key** as the card header (precedence: `.DisplayProperty("ProductName")` override → `[DisplayKey]` on the related entity, combined in order → conventional property `Name`/`Title`/`Description`/`DisplayName` → `Id`). When the related entity declares multiple `[DisplayKey]` properties, the lookup shows all of them joined (`" · "`); the type-ahead search still filters on the first display-key property. Entities without `[DisplayKey]` keep the previous single-property behavior unchanged.

### Collection Navigation (1-to-Many)

Properties of type `ICollection<T>` are treated as collection navigations and rendered as `ListPart` inside `BaseCard`:

```csharp
public ICollection<OrderItem> Items { get; set; } = [];
```

### Filtered Includes (Navigation Collection Filters)

When querying via `BaseQuery` or the fluent builder, you can filter and sort collection navigations server-side using `NavigationFilter`:

```csharp
var query = new BaseQuery
{
    Select = ["Id", "Name", "Items"],
    NavigationFilters =
    [
        new NavigationFilter
        {
            NavigationName = "Items",
            Filters = [new() { PropertyName = "IsActive", Operator = FilterOperator.Equals, Value = true }],
            Sorts = [new() { PropertyName = "DisplayOrder" }],
        },
    ],
};
```

This translates to EF Core's filtered include: `.Include(e => e.Items.Where(i => i.IsActive).OrderBy(i => i.DisplayOrder))`.

With the fluent builder this is much simpler — see [Fluent Query Builder](#fluent-query-builder).

`BaseList` also supports `NavigationFilters` as a parameter:

```html
<BaseList TModel="Order" NavigationFilters="@navFilters">
    ...
</BaseList>
```

### BaseLookupDialog

For advanced scenarios, `BaseLookupDialog<TModel>` provides a standalone modal dialog with search, pagination, and item selection:

```html
<BaseLookupDialog TModel="Category"
                  DataProvider="@CategoryProvider"
                  DisplayPropertyName="Name"
                  Title="Select Category"
                  OnItemSelected="@OnCategorySelected" />
```

---

## Field Security

Security can be defined via attributes on the model (enforced server-side) or via the fluent builder (UI-side).

### Attribute-Based Security

```csharp
// Class-level: Admin can do everything, regular users can only read
[BaseCrud("products")]
[CrudAccess("Admin", "RIMD")]
[CrudAccess("User", "R")]
public class Product
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    // Only Admin and Manager get read access; Admin can also modify
    [CrudAccess("Admin", "RM")]
    [CrudAccess("Manager", "R")]
    public decimal CostPrice { get; set; }

    // Visible to all, but only Admin can modify
    [CrudAccess("*", "R")]
    [CrudAccess("Admin", "RM")]
    public decimal RetailPrice { get; set; }

    // Completely excluded from API — no role gets any rights
    [CrudAccess("*", "")]
    public string InternalNotes { get; set; } = string.Empty;
}
```

**Rights characters:**
- `R` = Read · `I` = Insert · `M` = Modify · `D` = Delete
- Allowed at class level and on navigation properties; `D` on scalar properties throws `ArgumentException`.

**Hierarchy:** Class > Property > View. Each level can only further restrict — never expand — the rights granted above. Multiple matching attributes on the same level are combined via **union** of rights (a user with two matching roles gets the union of their rights).

**Server-side enforcement:**
- `GET` / `POST query`: Requires class-level `R`. Fields without property-level `R` are nulled in the response — including reference and collection navigations whose target class (or the navigation itself) is not readable, recursively. `POST query` additionally returns `403` when a filter or sort targets a field the caller may not read, so the query cannot be used as an oracle to leak non-readable values.
- `POST` (create): Requires class-level `I`. Body fields without property-level `I` cause `403`. The response is field-stripped the same way as `GET`/`query`.
- `PATCH`: Requires class-level `M`. Changed fields without property-level `M` cause `403`. Always-excluded fields (`[CrudAccess("*", "")]`) cause `400`. The response is field-stripped the same way as `GET`/`query`.
- `DELETE`: Requires class-level `D`.
- **Row-level scoping:** when `BaseEndpointOptions.UserFilter` is configured, `PATCH`/`DELETE` return `404` for rows outside the caller's scope (see [Registering Server Endpoints](#registering-server-endpoints)). The same predicate is applied server-side to `query`/`count`/`GET {id}` via the `scopeFilter` parameter of `IBaseDataProvider<TModel>` — it is a server-only mechanism, ignored by `HttpBaseDataProvider` and unsupported (`NotSupportedException`) by the Identity-backed `UserDataProvider`.

**Client-side enforcement:**
- `BaseList` hides columns without `R`, hides Add/Edit/Delete buttons by class-level `I`/`M`/`D`.
- `BaseCard` hides fields without `R`, renders fields without `M` as read-only, hides the Save button when neither `I` (for new items) nor `M` (for edits) is granted.
- `BaseListPart` hides Add when missing `I`, hides Delete when missing `D`.

### Fluent Builder Security

View-level rules can only further restrict (intersection with class/property rules):

```csharp
new BaseCardBuilder<Product>()
    .Field(x => x.CostPrice, f => f
        .Label("Cost Price")
        .Access("Admin", "RM")          // multiple .Access(...) calls combine via union
        .Access("Finance", "R")
    )
    .Build();
```

### Wildcard Role

Use `"*"` to match every user (including unauthenticated):

```csharp
[CrudAccess("*", "R")]   // everyone can read this field
public string Name { get; set; }
```

### Startup Role Validation (optional, server-side)

To verify at app startup that every role referenced by `[CrudAccess]` actually exists in the Identity role store, register the role validator:

```csharp
builder.Services.AddCrudAccessRoleValidation<IdentityRole>();
```

Throws `InvalidOperationException` with the list of unknown roles when the app starts.

---

## Optimistic Concurrency

### How It Works

1. **AuditModel entities**: `ModifiedOn` has `[ConcurrencyCheck]` and is used as the concurrency stamp.
2. **Non-audit entities**: Properties with `[ConcurrencyCheck]` or `[Timestamp]` are auto-detected by EF Core.
3. On save, `BaseCard` sends the concurrency stamp with the PATCH request.
4. If the entity was modified since last load, the server returns **409 Conflict**.
5. `BaseCard` catches the conflict and displays a user-friendly error message.

### User Experience

When a concurrency conflict occurs, a `FluentMessageBar` is shown:

> "This entry was modified by another user. Please reload to see the latest version."

The user's unsaved edits are preserved so they can copy their changes before reloading.

---

## Dynamic Field Selection

### How It Works

- `BaseList` automatically builds a `Select` list from column property names + `Id`.
- `BaseCard` requests all configured field properties + `Id` + FK properties.
- The server builds a dynamic EF Core `Select()` expression using `Expression.MemberInit`, returning only the requested fields.
- Projection expressions are cached per unique field combination in a `ConcurrentDictionary`.
- Navigation properties in `Select` are loaded via `.Include()`.

### Client-Side

`GetByIdAsync` supports an optional `select` parameter:

```csharp
// Only load Name and Price
var product = await provider.GetByIdAsync(id, select: ["Name", "Price"]);
```

`BaseQuery` includes a `Select` property for list queries:

```csharp
var query = new BaseQuery
{
    Select = ["Name", "Price"],
    Take = 50
};
```

---

## Fluent Query Builder

The `BaseQueryBuilder<T>` provides a LINQ-like fluent API for building `BaseQuery` objects. It automatically decomposes `Where()` lambda expressions into `FilterDescriptor` trees, supporting AND/OR logic.

### Getting Started

Start a query from any `IBaseDataProvider<T>` using the `.Query()` extension method:

```csharp
using BlazorBase.CRUD.Querying;

var result = await provider.Query()
    .Where(p => p.IsActive && p.Price > 10)
    .OrderBy(p => p.Name)
    .Select(p => p.Id, p => p.Name, p => p.Price)
    .Take(50)
    .ToListAsync();
```

### Available Methods

| Method | Description |
|---|---|
| `.Where(predicate)` | Filter with a lambda expression (supports `&&`, `\|\|`, comparisons, string methods) |
| `.OrderBy(selector)` | Sort ascending |
| `.OrderByDescending(selector)` | Sort descending |
| `.ThenBy(selector)` | Secondary sort ascending |
| `.ThenByDescending(selector)` | Secondary sort descending |
| `.Select(selectors...)` | Project specific fields |
| `.Include(navigation)` | Include a navigation property |
| `.Include<TNav>(navigation, configure)` | Include a collection navigation with filters/sorts |
| `.Skip(n)` | Skip n items |
| `.Take(n)` | Limit result count |
| `.Build()` | Build the `BaseQuery` without executing it |
| `.ToListAsync()` | Execute and return `BaseQueryResult<T>` |
| `.FirstOrDefaultAsync()` | Execute and return first item or null |

### Expression Decomposition

`Where()` predicates are automatically decomposed into `FilterDescriptor` trees:

| Expression | FilterDescriptor |
|---|---|
| `p.IsActive` | `PropertyName=IsActive, Op=Equals, Value=true` |
| `!p.IsActive` | `PropertyName=IsActive, Op=Equals, Value=false` |
| `p.Name == "x"` | `PropertyName=Name, Op=Equals, Value="x"` |
| `p.Name != "x"` | `PropertyName=Name, Op=NotEquals, Value="x"` |
| `p.Price > 10` | `PropertyName=Price, Op=GreaterThan, Value=10` |
| `p.Name.Contains("x")` | `PropertyName=Name, Op=Contains, Value="x"` |
| `p.Name.StartsWith("x")` | `PropertyName=Name, Op=StartsWith, Value="x"` |
| `p.Name == null` | `PropertyName=Name, Op=IsNull` |
| `p.Category.Name == "x"` | `PropertyName=Category.Name, Op=Equals, Value="x"` (dotted path) |
| `ids.Contains(p.CategoryId)` | `PropertyName=CategoryId, Op=In, Value=<the collection>` — translates to SQL `IN (…)` |
| `X && Y` | Group with `Logic=And` |
| `X \|\| Y` | Group with `Logic=Or` |

Captured variables are automatically resolved:

```csharp
var minPrice = 9.99m;
var result = await provider.Query()
    .Where(p => p.Price >= minPrice)
    .ToListAsync();
```

### AND / OR Filters

`FilterDescriptor` supports recursive grouping for complex conditions:

```csharp
var result = await provider.Query()
    .Where(p => p.IsActive && (p.Name.Contains("widget") || p.Price < 5))
    .ToListAsync();
```

This produces a filter tree:

```
AND
├── IsActive == true
└── OR
    ├── Name Contains "widget"
    └── Price < 5
```

Multiple `.Where()` calls are combined with AND:

```csharp
provider.Query()
    .Where(p => p.IsActive)
    .Where(p => p.Price > 10)   // AND'd with the first Where
```

### Including Filtered Navigation Collections

Use `.Include<TNav>()` with a builder lambda to filter and sort a collection navigation:

```csharp
var result = await provider.Query()
    .Where(o => o.IsActive)
    .OrderBy(o => o.Name)
    .Select(o => o.Id, o => o.Name)
    .Include<OrderItem>(o => o.Items, nav => nav
        .Where(i => i.IsActive)
        .OrderBy(i => i.DisplayOrder))
    .Take(100)
    .ToListAsync();
```

This generates an EF Core filtered include:

```csharp
.Include(o => o.Items.Where(i => i.IsActive).OrderBy(i => i.DisplayOrder))
```

Use `.Include()` (without type parameter) for unfiltered navigation properties:

```csharp
.Include(p => p.Category)   // simple Include without filtering
```

### Complete Example

```csharp
var types = await activityTypeProvider.Query()
    .Where(t => t.IsActive)
    .OrderBy(t => t.DisplayOrder)
    .Select(t => t.Id, t => t.Name, t => t.Color, t => t.DisplayOrder, t => t.IsActive)
    .Include<ActivitySubtype>(t => t.Subtypes, nav => nav
        .Where(s => s.IsActive)
        .OrderBy(s => s.DisplayOrder))
    .Take(1000)
    .ToListAsync();

var current = await activityProvider.Query()
    .Where(a => a.EndTime == null)
    .OrderByDescending(a => a.StartTime)
    .Include(a => a.ActivityType)
    .Include(a => a.ActivitySubtype)
    .FirstOrDefaultAsync();
```

---

## Localization

Column titles (`BaseList`) and field labels (`BaseCard`) are resolved with a 3-step fallback:

1. **Explicit value** — `.Title("...")` / `.Label("...")` from configuration or declarative markup.
2. **Explicit Localizer** — `IStringLocalizer` passed via the `Localizer` parameter on `BaseList` / `BaseCard`.
3. **Model Localizer** — `IStringLocalizer<TModel>` resolved automatically from DI via `IStringLocalizerFactory`.
4. **Property name** — The raw C# property name (e.g., `"CustomerName"`).

This means you can skip `.Title()` / `.Label()` entirely if you provide a resource file for the model class:

```csharp
// Product.resx / Product.en.resx
// Key: Name       → Value: "Product Name"
// Key: Price      → Value: "Price"
// Key: CategoryId → Value: "Category"
```

```csharp
// No Title/Label needed — resolved from Product.resx automatically
new BaseListBuilder<Product>()
    .Column(x => x.Name, c => c.Sortable().Filterable())
    .Column(x => x.Price, c => c.Format("C2").Sortable())
    .Build();
```

### Passing an Explicit Localizer

If you want column titles / field labels to come from the **page's** resource file instead of the model's:

```html
@inject IStringLocalizer<ProductList> Localizer

<BaseList TModel="Product" Localizer="@Localizer" />
<BaseCard TModel="Product" Localizer="@Localizer" Model="@item" />
```

The explicit `Localizer` parameter takes precedence over the auto-resolved model localizer.

---

## Interceptors — Lifecycle Hooks

Interceptors hook into the CRUD lifecycle at the DataProvider level.

### Implementation

```csharp
public class ProductInterceptor : BaseDataInterceptor<Product>
{
    public override Task<Product> OnBeforeCreateAsync(
        Product model, CancellationToken cancellationToken = default)
    {
        model.Name = model.Name.Trim();
        return Task.FromResult(model);
    }

    public override Task<Dictionary<string, object?>> OnBeforePatchAsync(
        object id, Dictionary<string, object?> changedFields, CancellationToken cancellationToken = default)
    {
        // Modify or validate changed fields before applying
        if (changedFields.TryGetValue("Name", out var name) && name is string nameStr)
            changedFields["Name"] = nameStr.Trim();

        return Task.FromResult(changedFields);
    }

    public override Task<BaseQuery> OnBeforeQueryAsync(
        BaseQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Sorts.Count == 0)
            query.Sorts.Add(new SortDescriptor { PropertyName = "Name", Direction = SortDirection.Ascending });

        return Task.FromResult(query);
    }
}
```

### Registration

```csharp
builder.Services.AddBaseDataInterceptor<Product, ProductInterceptor>();
```

### Available Hooks

| Hook | Return Type | Description |
|---|---|---|
| `OnBeforeCreateAsync(model)` | `Task<TModel>` | Before creation — model can be modified |
| `OnAfterCreateAsync(model)` | `Task` | After creation |
| `OnBeforePatchAsync(id, changedFields)` | `Task<Dictionary<string, object?>>` | Before patch — changed fields can be modified |
| `OnAfterPatchAsync(model)` | `Task` | After patch |
| `OnBeforeDeleteAsync(id, model)` | `Task` | Before deletion |
| `OnAfterDeleteAsync(id)` | `Task` | After deletion |
| `OnBeforeQueryAsync(query)` | `Task<BaseQuery>` | Before any query — filters/sorts can be modified |

---

## Validation

### DataAnnotations (Built-in)

```csharp
[BaseCrud("products")]
public class Product
{
    [Required(ErrorMessage = "Name is required")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, 999999.99, ErrorMessage = "Price must be positive")]
    public decimal Price { get; set; }
}
```

DataAnnotations are automatically validated by the `EditForm` inside `BaseCard`.

### Custom Validators

For complex validation logic (e.g. database queries, cross-field validation):

```csharp
public class ProductValidator(IBaseDataProvider<Product> productProvider) : IBaseValidator<Product>
{
    public async Task<IEnumerable<ValidationResult>> ValidateAsync(
        Product model, CancellationToken cancellationToken = default)
    {
        var results = new List<ValidationResult>();

        var existing = await productProvider.GetListAsync(new BaseQuery
        {
            Filters = [new FilterDescriptor { PropertyName = "Name", Operator = FilterOperator.Equals, Value = model.Name }]
        }, cancellationToken: cancellationToken);

        if (existing.Items.Any(p => p.Id != model.Id))
            results.Add(new ValidationResult("A product with this name already exists.", [nameof(model.Name)]));

        return results;
    }
}
```

### Registration

```csharp
builder.Services.AddBaseValidator<Product, ProductValidator>();
```

Custom validators are automatically invoked by `BaseCard` on submit. Validation errors are displayed in the `FluentMessageBar`.

---

## Audit Fields

### AuditModel

Entities that inherit from `AuditModel` automatically get their audit fields populated:

```csharp
[BaseCrud("orders")]
public class Order : AuditModel
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    // CreatedOn, CreatedBy, ModifiedOn, ModifiedBy are populated automatically
    // ModifiedOn has [ConcurrencyCheck] for optimistic concurrency
}
```

### IAuditUserProvider

To populate `CreatedBy` and `ModifiedBy` with the current user, implement `IAuditUserProvider`:

```csharp
public class MyAuditUserProvider(IHttpContextAccessor httpContextAccessor) : IAuditUserProvider
{
    public string? GetCurrentUserId()
        => httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
```

### Registration

```csharp
builder.Services.AddBlazorBaseCrud<MyAuditUserProvider>();
```

### Behavior

| EntityState | Field | Value |
|---|---|---|
| `Added` | `CreatedOn` | `DateTime.UtcNow` |
| `Added` | `CreatedBy` | `IAuditUserProvider.GetCurrentUserId()` |
| `Modified` | `ModifiedOn` | `DateTime.UtcNow` |
| `Modified` | `ModifiedBy` | `IAuditUserProvider.GetCurrentUserId()` |

Audit fields are set in `BaseSaveChangesInterceptor`, which is injected into the DbContext factory as an EF Core `SaveChangesInterceptor`.

### Server-Authoritative

Audit fields are fully server-authoritative and cannot be set or corrected through the provider or the REST endpoint:
- `BaseSaveChangesInterceptor` always sets `CreatedOn`/`CreatedBy` on insert and `ModifiedOn`/`ModifiedBy` on update, overwriting any client-supplied values.
- On update, it explicitly reverts `CreatedOn`/`CreatedBy` back to their original (unmodified) database values, even if a client attempted to change them.
- `DbContextBaseDataProvider<T>.PatchAsync` never applies `AuditModel` fields from a PATCH payload — changes to them in `PatchModel.ChangedFields` are silently ignored.

---

## Source Generator (DTO)

The `BlazorBase.CRUD.Generators` package can generate DTOs and mapping extensions for scenarios that require them.

> **Note:** With the Entity = Model architecture, DTOs are no longer required for standard CRUD operations. The generator is available for advanced scenarios where a separate DTO shape is needed.

### Usage

```csharp
[BaseEntity(DtoName = "ProductDto", ExcludeProperties = new[] { "InternalField" })]
public class ProductEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string InternalField { get; set; } = string.Empty;
}
```

This generates:

```csharp
// Auto-generated
public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

public static class ProductEntityMappingExtensions
{
    public static ProductDto ToDto(this ProductEntity entity) => ...;
    public static ProductEntity ToEntity(this ProductDto dto) => ...;
}
```

---

## API Reference

### Components

| Component | Description |
|---|---|
| `BaseList<TModel>` | List view with FluentDataGrid, infinite scroll, filtering, sorting |
| `BaseCard<TModel>` | Detail view / form with dirty tracking and concurrency |
| `BaseListPart<TModel>` | Embedded child collection inside a BaseCard |
| `BaseLookupDialog<TModel>` | Modal dialog for selecting related entities |
| `BaseColumn<TModel>` | Declarative column definition (child of BaseList) |
| `PropertyField<TModel>` | Declarative field definition (child of BaseCard) |
| `ListPartField<TModel>` | Declarative list part definition (child of BaseCard) |
| `BaseDialog<TModel>` | Modal dialog variant of a BaseCard |
| `BaseActionGroup<TModel>` | Declarative action group definition (child of BaseList / BaseCard) |
| `BaseAction<TModel>` | Declarative action definition (child of BaseActionGroup) |
| `BaseActionToolbar<TModel>` | Renders action groups as toolbar buttons / dropdown menus |

### Attributes

| Attribute | Target | Description |
|---|---|---|
| `[BaseCrud("route")]` | Class | Marks entity for auto-registration |
| `[CrudAccess(roles, rights)]` | Class & Property | Grants R/I/M/D rights to one or more roles. Multiple allowed. Use `[CrudAccess("*", "")]` to exclude a property from all CRUD operations. |

### Interfaces

| Interface | Description |
|---|---|
| `IBaseDataProvider<TModel>` | Abstraction for CRUD operations |
| `IBaseDataInterceptor<TModel>` | Lifecycle hooks for DataProvider |
| `IBaseValidator<TModel>` | Custom validation logic |
| `IAuditUserProvider` | Provides the current user ID for audit fields |
| `ICrudActionComponent<TModel>` | Interface for dynamic action components (receives `Args`, fires `OnClose`) |

### DI Extensions

| Method | Description |
|---|---|
| `AddBlazorBaseCrud()` | Register core (host-agnostic) services |
| `AddBlazorBaseCrud<T>()` | Core + AuditUserProvider |
| `AddBlazorBaseCrudComponents()` | Register the interactive UI services the CRUD components depend on (`IConfirmationService`, default `FluentUiConfirmationService`, needs the FluentUI `IDialogService`). Interactive-rendering hosts only (Wasm/Maui/Blazor-Server-interactive), not the pure server; call after `AddFluentUIComponents()` |
| `AddBaseDbContext<TContext>(...)` | DbContext factory with interceptors |
| `AddBlazorBaseCrudServer<TContext>(Assembly?)` | Assembly scan: register DbContext providers |
| `AddBlazorBaseCrudClient(Action<HttpClient>, Assembly?)` | Assembly scan: register HTTP providers |
| `AddCrudAccessRoleValidation<TRole>(Assembly?)` | Register a hosted service that validates referenced roles at startup |
| `AddBaseHttpDataProvider<T>(...)` | Manual HTTP DataProvider registration |
| `AddBaseDataInterceptor<T, I>()` | Register an interceptor |
| `AddBaseValidator<T, V>()` | Register a validator |
| `AddBlazorBaseCustomInput<TComponent>()` | Register a custom card input component (scoped, first matching `CanHandle` wins) |
| `AddBlazorBaseCustomDisplay<TComponent>()` | Register a custom list-cell display component (scoped, first matching `CanHandle` wins) |
| `MapBlazorBaseCrudEndpoints(Assembly?)` | Assembly scan: map all CRUD endpoints |
| `MapBlazorBaseCrudEndpoints<T>(route, ...)` | Manual: map endpoints for one entity |

### IBaseDataProvider Methods

| Method | Description |
|---|---|
| `GetListAsync(BaseQuery, scopeFilter?)` | Query with filtering, sorting, pagination, field selection |
| `GetByIdAsync(id, select?, scopeFilter?)` | Get single entity, optionally with field selection |
| `GetCountAsync(scopeFilter?)` | Total entity count |
| `CreateAsync(model)` | Create new entity |
| `PatchAsync(id, changedFields, stamp?)` | Update only changed fields with concurrency |
| `DeleteAsync(id)` | Delete entity |
| `Query()` | Start a fluent query builder (extension method) |

> `scopeFilter` (all three read methods, `Expression<Func<TModel, bool>>?`, default `null`) is a **server-only** row-level-security predicate: `DbContextBaseDataProvider<T>` applies it, `HttpBaseDataProvider<T>` ignores it, and the Identity-backed `UserDataProvider` throws `NotSupportedException` if a non-null value is passed. It sits before `CancellationToken` in each signature — callers that passed `CancellationToken` positionally must switch to `cancellationToken:`. See [Field Security](#field-security) for `BaseEndpointOptions.UserFilter`, the per-endpoint mechanism that supplies it automatically on the server.

### Query Model

| Class | Description |
|---|---|
| `BaseQuery` | Query with `Filters`, `Sorts`, `Select`, `NavigationFilters`, `Skip`, `Take` |
| `BaseQueryResult<T>` | Result with `Items` and `TotalCount` |
| `PatchModel` | PATCH body: `ChangedFields` + `ConcurrencyStamp` |
| `FilterDescriptor` | Filter: `PropertyName`, `Operator`, `Value` — or group: `Logic`, `Filters` |
| `FilterLogic` | `And`, `Or` |
| `SortDescriptor` | Sort: `PropertyName`, `Direction` |
| `FilterOperator` | `Contains`, `Equals`, `NotEquals`, `GreaterThan`, `LessThan`, `GreaterThanOrEqual`, `LessThanOrEqual`, `StartsWith`, `EndsWith`, `IsNull`, `IsNotNull`, `In` |
| `NavigationFilter` | Filtered include: `NavigationName`, `Filters`, `Sorts` |
| `CrudAction<TModel>` | Action definition: `Caption`, `Icon`, `Contexts`, `Visible`, `Action`, `IsBulkAction` |
| `CrudActionGroup<TModel>` | Named group of actions: `Name`, `Caption`, `Icon`, `Contexts`, `Actions` |
| `CrudActionContext` | Flags enum: `ListToolbar`, `ContextMenu`, `Card`, `ListPart`, `All` |
| `CrudActionEventArgs<TModel>` | Action callback args: `Item`, `SelectedItems`, `ServiceProvider`, `Context` |
| `CrudActionVisibilityArgs<TModel>` | Visibility predicate args: `Item`, `User`, `Context` |
| `CrudActionComponentConfig<TModel>` | Dynamic component config: `ComponentType`, `Parameters`, `OnComponentClosed` |

### Querying

| Class | Description |
|---|---|
| `BaseQueryBuilder<T>` | Fluent query builder — `.Where()`, `.OrderBy()`, `.Select()`, `.Include()`, `.ToListAsync()` |
| `NavigationFilterBuilder<TNav>` | Fluent builder for navigation filters — `.Where()`, `.OrderBy()` |
| `FilterExpressionDecomposer` | Decomposes lambda predicates into `FilterDescriptor` trees |
| `BaseQueryBuilderExtensions` | Extension method: `IBaseDataProvider<T>.Query()` |

---

## Best Practices

1. **Decorate entities with `[BaseCrud]`** — Assembly scanning eliminates boilerplate registration code.
2. **Use `[CrudAccess]` for security** — Class- and property-level rights are enforced both server-side (endpoint guards, response stripping) and client-side (component visibility/read-only state). Hierarchy: Class > Property > View, with intersection at each level.
3. **Interceptors for server-side logic, events for UI feedback** — UI events (`OnBeforeSave` etc.) are for UI feedback; interceptors are for business logic.
4. **Mind the `new()` constraint** — `BaseCard<TModel>` and `BaseListPart<TModel>` require `where TModel : class, new()`.
5. **Let navigation properties auto-detect** — FK conventions like `CategoryId` + `Category` are discovered automatically. Use `[ForeignKey]` only for non-standard names.
6. **Responsive Grid** — `MaxColumns` on `BaseCard` controls the maximum number of columns. The CSS grid automatically wraps on smaller screens.
7. **Only send what changed** — `BaseCard` automatically tracks dirty fields and sends `PatchAsync` with only the modified values.

---

## RichTextEditor Component

`BlazorBase.CRUD.Components.RichTextEditor.RichTextEditor` is a generic, vendored WYSIWYG editor that edits and produces an HTML string. No external packages or CDN resources are required — the JS module ships as a static web asset of this RCL.

### Parameters

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `Value` | `string?` | `null` | Current HTML value (two-way bindable via `@bind-Value`). |
| `ValueChanged` | `EventCallback<string?>` | — | Raised ~200 ms after typing stops. |
| `Placeholder` | `string?` | `null` | Placeholder text for the empty surface. |
| `ReadOnly` | `bool` | `false` | Makes the surface read-only via CSS + `contenteditable=false`. |
| `Disabled` | `bool` | `false` | Visual disabled state (also sets read-only). |
| `Label` | `string?` | `null` | Optional label above the toolbar. |
| `BoldLabel` | `string` | `"Bold"` | `title` + `aria-label` for the Bold button. |
| `ItalicLabel` | `string` | `"Italic"` | `title` + `aria-label` for the Italic button. |
| `UnderlineLabel` | `string` | `"Underline"` | `title` + `aria-label` for the Underline button. |
| `BulletListLabel` | `string` | `"Bulleted list"` | `title` + `aria-label` for the Bulleted list button. |
| `NumberedListLabel` | `string` | `"Numbered list"` | `title` + `aria-label` for the Numbered list button. |
| `InsertLinkLabel` | `string` | `"Insert link"` | `title` + `aria-label` for the Insert link button. |
| `ClearFormattingLabel` | `string` | `"Clear formatting"` | `title` + `aria-label` for the Clear formatting button. |
| `InsertLinkPromptText` | `string` | `"Enter URL:"` | Text shown in the browser prompt when Insert link is activated. |

Implements `IAsyncDisposable`. Multiple instances coexist on the same page via per-instance element ids (`rte-{Guid:N}`).

### Toolbar accessibility

All toolbar buttons carry `title` and `aria-label` bound from the parameters above. Keyboard activation (Tab to a button + Enter or Space) works: `mousedown` only calls `preventDefault()` to preserve the editor selection; the formatting command fires on `click`. The root element has `aria-disabled` reflecting `ReadOnly` or `Disabled`.

### Link scheme validation

The prompted URL for Insert link is validated against `http:`, `https:`, and `mailto:` (case-insensitive). Anything else — including `javascript:` and `data:` — is rejected.

### Value re-sync

External changes to `Value` after first render are pushed into the editor DOM. A `LastSyncedValue` field prevents echoing edits that originated in the editor itself.

### Toolbar

Bold · Italic · Underline · Bulleted list · Numbered list · Insert link · Clear formatting — via `document.execCommand`. The JS module (`wwwroot/js/richTextEditor.js`) is replaceable behind the same named-export contract.

### Theming

CSS isolation (`RichTextEditor.razor.css`); colors come from FluentUI CSS custom properties. No hard-coded colors.

### Read-only / disabled state

CSS class `rte-disabled` is added to the root when `ReadOnly` or `Disabled` is true. The stylesheet rule `.rte-disabled .rte-toolbar { pointer-events: none }` disables toolbar interaction — no imperative JS style writes. The root element carries `aria-disabled`.

### Sanitization

The editor does not sanitize HTML. Sanitize server-side before persistence using `IHtmlSanitizer` (see below) in an `IBaseDataInterceptor<T>`. Display stored HTML with `SanitizedHtml` (see below).

### Basic usage

```razor
<RichTextEditor @bind-Value="@Model.Body" Label="Body" Placeholder="Enter description…" />
```

### Localized toolbar labels

```razor
<RichTextEditor @bind-Value="@Model.Body"
                BoldLabel="@Localizer["Bold"]"
                ItalicLabel="@Localizer["Italic"]"
                InsertLinkPromptText="@Localizer["InsertLinkPrompt"]" />
```

### HtmlField convenience (recommended)

The one-call way to wire a rich-text editor + sanitized display into a `BaseCard` field:

```csharp
new BaseCardBuilder<Article>()
    .Field(a => a.Body, f => f.Label("Body").ColSpan(2).HtmlField())
    .Build();
```

### Manual EditorTemplate / DisplayTemplate

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

`FieldBuilder<TModel>` exposes `EditorTemplate(RenderFragment<PropertyFieldContext<TModel>>)`, `DisplayTemplate(RenderFragment<PropertyFieldContext<TModel>>)`, and `HtmlField()`. `BasePropertyInput` renders `EditorTemplate` when editing and `DisplayTemplate` when in display mode.

---

## SanitizedHtml Component

`BlazorBase.CRUD.Components.SanitizedHtml.SanitizedHtml` renders an HTML string safely inside a `<div class="sanitized-html">` root element.

### Parameters

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `Value` | `string?` | `null` | The HTML to display. |

### Behavior

- When `IHtmlSanitizer` is registered in DI, the value is sanitized before being rendered as a `MarkupString`.
- When no `IHtmlSanitizer` is registered, the value is HTML-encoded and rendered as plain text — **never as raw markup** — so the component is XSS-safe by default.

### Usage

```razor
<SanitizedHtml Value="@storedHtml" />
```

---

## DiffViewer Component

`BlazorBase.CRUD.Components.DiffViewer.DiffViewer` renders a single-file unified diff in three modes with optional syntax highlighting via the vendored highlight.js 11.9.0 (common-languages build, BSD-3 license, no CDN).

### Parameters

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `Diff` | `FileDiff?` | `null` | The diff to render. Null or empty hunks shows `EmptyDiffLabel`. |
| `Mode` | `DiffViewMode` | `Inline` | `Inline`, `SideBySide`, or `NewOnly`. Two-way bindable via `@bind-Mode`. |
| `ModeChanged` | `EventCallback<DiffViewMode>` | — | Raised when the user clicks a mode button. |
| `ShowModeSwitch` | `bool` | `true` | Renders the three mode-switch buttons above the diff. |
| `Language` | `string?` | `null` | highlight.js language hint (e.g. `"csharp"`). Null = auto-detect. |
| `EnableSyntaxHighlight` | `bool` | `true` | Calls highlight.js after render. |
| `WrapLines` | `bool` | `false` | Wraps long lines in code cells. |
| `MaxRenderedLines` | `int` | `5000` | Line count cap; shows `LargeDiffLabel` when exceeded. |
| `InlineModeLabel` | `string` | `"Inline"` | Mode button label. |
| `SideBySideModeLabel` | `string` | `"Side by side"` | Mode button label. |
| `NewOnlyModeLabel` | `string` | `"New only"` | Mode button label. |
| `BinaryFileLabel` | `string` | `"Binary file not shown"` | Shown when `Diff.IsBinary`. |
| `LargeDiffLabel` | `string` | `"Diff too large to display"` | Shown when total lines > `MaxRenderedLines`. |
| `EmptyDiffLabel` | `string` | `"No changes"` | Shown when `Diff` is null or has no hunks. |
| `FilePath` | `string?` | `null` | File path echoed into every `DiffLineCommentContext`. Purely informational — the host uses it to identify which file a comment belongs to. |
| `LineCommentTemplate` | `RenderFragment<DiffLineCommentContext>?` | `null` | When set, an extra full-width row is inserted immediately below every diff line. The host controls content — return empty content for lines without a comment thread. `null` = no extra rows (backward-compatible default). |
| `OnAddLineComment` | `EventCallback<DiffLineCommentContext>` | — | When set, a small "+" gutter button appears on each diff line (visible on hover or keyboard focus, always keyboard-reachable). Not set = no gutter column (backward-compatible default). |
| `AddCommentLabel` | `string` | `"Add comment"` | `aria-label` text for the per-line gutter button. Localizable by the host. |

Implements `IAsyncDisposable`. Root element id is `diff-{Guid:N}` (scopes the JS highlighter). Requires `[Inject] IJSRuntime`.

### View modes

- **Inline**: one table per hunk — old line number | new line number | content; row classes `diff-row--added`, `diff-row--removed`, `diff-row--context`.
- **SideBySide**: paired old | new columns per hunk; removed lines on the left, added on the right, padded with empty cells when counts differ; cell classes `diff-cell--added`, `diff-cell--removed`, `diff-cell--empty`.
- **NewOnly**: only `Added` lines; one table per hunk with new line numbers.

### XSS safety

Content cells are bound with `@line.Content` (Razor encodes to text — never `MarkupString`). The `[data-code]` attribute lets the JS module locate cells; `highlightAll` reads each cell's existing `textContent` (already encoded) and passes it to `hljs.highlight()`/`hljs.highlightAuto()`, whose output (only `<span class="hljs-...">` wrappers) is assigned to `innerHTML`. No server-provided HTML is ever injected.

### Syntax highlighting

highlight.js 11.9.0 is vendored at `wwwroot/lib/highlight/highlight.min.js` (common-languages build; includes csharp, javascript, typescript, json, css, html, xml, bash/sh, sql, markdown, and ~40 others). The dark theme CSS (`github-dark.min.css`) is injected once into `<head>` by `syntaxHighlight.js` when the component first highlights — no consumer-side `<link>` required. `SyntaxHighlightInterop` is the per-instance C# wrapper (lazy JS module load, `IAsyncDisposable`, mirrors `RichTextEditorInterop`).

### `DiffLineCommentContext` record

```csharp
namespace BlazorBase.CRUD.Components.DiffViewer;

public record DiffLineCommentContext(
    string? FilePath,
    int? OldLineNumber,
    int? NewLineNumber,
    DiffLineKind Kind);
```

Passed to `OnAddLineComment` and as the generic argument of `LineCommentTemplate`. `FilePath` echoes `DiffViewer.FilePath`. `OldLineNumber`/`NewLineNumber` are null when the line has no position on that side (e.g. a pure-added line has no `OldLineNumber`). Works consistently across all three view modes — in SideBySide mode the context is built from the new-side line when present, falling back to the old-side line.

### Backward compatibility

All three new parameters default to no-ops (`null` / no delegate). Existing call sites that do not set them render identically to before: no gutter column, no extra rows.

### Line-comment hosting pattern

`DiffViewer` renders the `LineCommentTemplate` row for every line and passes the `DiffLineCommentContext`. The host decides whether to produce content — return an empty fragment for lines without a thread. This keeps `DiffViewer` generic and the host in full control of which lines have threads.

```razor
<DiffViewer Diff="@myFileDiff"
            FilePath="@selectedFilePath"
            OnAddLineComment="@HandleAddLineComment"
            LineCommentTemplate="@RenderLineComments"
            AddCommentLabel="Add comment" />
```

### Basic usage

```razor
<DiffViewer Diff="@myFileDiff" Language="csharp" />
```

### Two-way mode binding

```razor
<DiffViewer Diff="@myFileDiff" @bind-Mode="@CurrentMode" />
```

---

## FileTree Component

`BlazorBase.CRUD.Components.FileTree.FileTree` renders an ARIA-compliant navigable file tree from a list of `FileTreeNode` roots. Supports expand/collapse, single-node selection, and keyboard navigation.

### Parameters

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `Nodes` | `IReadOnlyList<FileTreeNode>` | `[]` | Root nodes. Build from flat paths with `FileTreeBuilder.Build(paths)`. |
| `SelectedPath` | `string?` | `null` | Currently selected path (two-way bindable via `@bind-SelectedPath`). |
| `SelectedPathChanged` | `EventCallback<string>` | — | Raised when a file node is selected. |
| `OnNodeSelected` | `EventCallback<FileTreeNode>` | — | Raised with the full node when a file is selected. |
| `ExpandRootByDefault` | `bool` | `true` | Root-level directories start expanded. |
| `EmptyLabel` | `string` | `"No files"` | Shown when `Nodes` is empty. |

ARIA: root element `role="tree"`, each node `role="treeitem"` with `aria-expanded` (directories) and `aria-selected`. Children grouped under `role="group"`. The focused node carries `tabindex="0"`; all other nodes carry `tabindex="-1"` (roving tabindex).

### Keyboard navigation

Implements the [WAI-ARIA tree pattern](https://www.w3.org/WAI/ARIA/apg/patterns/treeview/):

| Key | Behavior |
|---|---|
| `Enter` / `Space` | Select a file node; toggle a directory node |
| `ArrowDown` | Move focus to the next visible node |
| `ArrowUp` | Move focus to the previous visible node |
| `ArrowRight` | Expand a collapsed directory; move focus to the first child if already expanded |
| `ArrowLeft` | Collapse an expanded directory; move focus to the parent node if already collapsed |

### Basic usage

```razor
@inject IGitReadApiService GitReadApi

<FileTree Nodes="@Nodes" @bind-SelectedPath="@SelectedPath" OnNodeSelected="@OnFileSelected" />

@code {
    private IReadOnlyList<FileTreeNode> Nodes = [];
    private string? SelectedPath;

    protected override async Task OnInitializedAsync()
    {
        var entries = await GitReadApi.GetTreeAsync(ProjectId, RepoId, "main");
        Nodes = FileTreeBuilder.Build(entries.Select(e => e.Path));
    }

    private async Task OnFileSelected(FileTreeNode node) { /* load file content */ }
}
```

---

## Diff Models (`BlazorBase.CRUD.Models.Diff`)

Pure data records/enums shared between the diff viewer and any server-side parser. JSON wire format is identical to the former `DevPortal.Shared.Modules.Repositories.Diff` namespace (only the namespace moved).

```csharp
namespace BlazorBase.CRUD.Models.Diff;

public record FileDiff(string Path, string? OldPath, FileChangeKind ChangeKind, bool IsBinary, IReadOnlyList<DiffHunk> Hunks);
public record DiffHunk(string Header, int OldStart, int OldLines, int NewStart, int NewLines, IReadOnlyList<DiffLine> Lines);
public record DiffLine(DiffLineKind Kind, int? OldLineNumber, int? NewLineNumber, string Content);
public enum DiffLineKind { Context, Added, Removed }
public enum FileChangeKind { Added, Modified, Deleted, Renamed, Copied, TypeChanged }
```

---

## FileTree Models (`BlazorBase.CRUD.Models.FileTree`)

```csharp
namespace BlazorBase.CRUD.Models.FileTree;

public sealed class FileTreeNode
{
    public required string Name { get; init; }   // last path segment
    public required string Path { get; init; }   // full '/'-separated path from root
    public bool IsDirectory { get; init; }
    public IReadOnlyList<FileTreeNode> Children { get; init; } = [];
}

public static class FileTreeBuilder
{
    // Converts a flat list of '/'-separated paths to a node hierarchy.
    // Directories appear before files; siblings are sorted by name (case-insensitive).
    public static IReadOnlyList<FileTreeNode> Build(IEnumerable<string> paths);
}
```

---

## IHtmlSanitizer

`BlazorBase.CRUD.Sanitization.IHtmlSanitizer` is an interface-only abstraction for HTML sanitization. No concrete implementation or sanitizer package is shipped here.

```csharp
namespace BlazorBase.CRUD.Sanitization;

public interface IHtmlSanitizer
{
    /// <summary>Sanitizes html; never returns null; returns "" for null input.</summary>
    string Sanitize(string? html);
}
```

- `SanitizedHtml` resolves this interface optionally from DI; falls back to HTML-encoding when absent.
- Register the concrete implementation only in the server project (never in WASM/MAUI clients).

```csharp
builder.Services.AddSingleton<IHtmlSanitizer, HtmlSanitizerAdapter>();
```

Implement in the consuming server project (e.g. wrapping `Ganss.Xss.HtmlSanitizer`) and register as a singleton:

```csharp
builder.Services.AddSingleton<IHtmlSanitizer, HtmlSanitizerAdapter>();
```

Inject into an `IBaseDataInterceptor<T>` to sanitize HTML fields before persistence.

---

## BaseValidationException

`BlazorBase.CRUD.Models.BaseValidationException` lets an `IBaseDataInterceptor<TModel>` signal a business-rule violation that the user should see as a friendly message rather than a generic error.

### How it works — end to end

| Layer | What happens |
|---|---|
| Interceptor | `throw new BaseValidationException("The Area does not belong to the selected Project.")` |
| `BaseEndpointMapper` | Catches `BaseValidationException`, returns `400 Bad Request` with body `{ error: "validation", message: "…" }` |
| `HttpBaseDataProvider` | Detects `400 BadRequest` on create/patch/delete, reads the `message` field, re-throws as `BaseValidationException` |
| `BaseCard.OnValidSubmitAsync` | Catches `BaseValidationException`, sets `ErrorMessage = ex.Message`, calls `StateHasChanged()` |
| UI | `FluentMessageBar` shows the message — same visual treatment as a concurrency conflict |

This path is **strictly additive**: behavior for all other exception types (`ConcurrencyConflictException`, `UnauthorizedAccessException`, generic exceptions) is unchanged.

### Constructors

```csharp
// Message only
throw new BaseValidationException("The Area does not belong to the selected Project.");

// Message + inner exception
throw new BaseValidationException("Sanitized HTML exceeds the 1 MB size limit.", originalException);
```

### Example

```csharp
public class WorkitemInterceptor : BaseDataInterceptor<Workitem>
{
    public override Task<Workitem> OnBeforeCreateAsync(
        Workitem model, CancellationToken cancellationToken = default)
    {
        if (model.AreaId.HasValue && model.Area?.ProjectId != model.ProjectId)
            throw new BaseValidationException(
                "The selected Area does not belong to the chosen Project.");

        return Task.FromResult(model);
    }

    public override Task<Dictionary<string, object?>> OnBeforePatchAsync(
        object id, Dictionary<string, object?> changedFields,
        CancellationToken cancellationToken = default)
    {
        if (changedFields.TryGetValue(nameof(Workitem.Description), out var html)
            && html is string htmlStr && htmlStr.Length > 1_048_576)
        {
            throw new BaseValidationException(
                "The description exceeds the maximum allowed size.");
        }

        return Task.FromResult(changedFields);
    }
}
```
