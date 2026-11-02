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
// call after AddBlazorBaseFluentUI()
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

Without the providers, opening an item or pressing *add* throws and the component tree fails
to render.

`BlazorBase.User`'s `BaseLayout` already carries the full set (`FluentProviders`), so a host that uses it needs nothing
further. **A host with its own layout has to add them itself**, together with `AddBlazorBaseFluentUI()` in the
service registration:

```razor
@* At the end of MainLayout.razor, outside the layout markup *@
<FluentProviders />
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

### Primary key

The components, the data providers and the endpoints address a row by the single-property primary key that
`EntityKeyResolver` (`BlazorBase.CRUD.Core`) resolves: a property named `Id`, otherwise the property marked
`[Key]`, otherwise `<TypeName>Id` (the EF Core conventions). A composite key resolves to nothing, and a type without a
key is addressed by reference only. `EntityKeyResolver.Find(type)` returns the property, `GetName(type)` its name
(`Id` when none resolves) and `GetValue(entity)` the value. The list selects the key with every query, the card
patches by it, the delete action and the deep link (`?item=<key>`) use it, and the `{id}` of the route is parsed to its type.

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

Renders a `FluentDataGrid` with sorting, filtering, virtualized scrolling, row selection and open, multi-select, context menus and a configurable action toolbar.

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
| `PageSize` | `int` | Rows per request to the data provider (the fetch chunk of the virtualized list, 10 to `BaseQueryLimits.MaxPageSize`). Default: `50`; see "Virtualization and paging" below |
| `DefaultSorts` | `List<SortDescriptor>?` | The order shown until the user sorts; see "Order and paging" below |
| `FilterDefaults` | `ListFilterDefaults?` | Presets, default filter and storage key declared in code; see "Saved filters, presets and defaults" below |
| `EnableSavedFilters` | `bool` | Lets users save/load their own filters when an `IListFilterStore` is registered. Default: `true` |
| `EnableDeepLinking` / `DeepLinkParameterName` | `bool` / `string` | Ties the card dialog to the URL (`?item=<id>`). Default: `true` / `"item"`; see "Editing a row" below |
| `MobileLayout` | `BaseListMobileLayout` | `Auto` (default): tiles below the breakpoint, table above; `Always`; `Never`. See "Smartphone tiles" below |
| `MobileBreakpoint` | `int?` | Container width in px below which tiles replace the table. Default `700` (`MobileCardPlanResolver.DefaultBreakpoint`); `0` keeps the table |
| `MobileCard` | `MobileCardConfiguration<TModel>?` | Which properties a tile shows and the part each plays (also `BaseListConfiguration.MobileCard`) |
| `MobileTemplate` | `RenderFragment<TModel>?` | Custom markup for the body of a tile |
| `MobileItemHeight` | `int?` | Height in px of every tile. Default: worked out from what the tile shows, 120 for a custom body; see "Smartphone tiles" |
| `OpenColumn` | `bool` | The first visible data column (or the one with `OpensItem`) is the open link of a row. Default: `true`; see "Selecting and opening rows" |
| `AllowView` | `bool` | A user who may read but not edit opens a row in a read-only card. Uses the `CardConfiguration`, else a card built from the visible columns; a `CardType` needs a `ReadOnly` parameter. Default: `true` |
| `SelectAllLimit` | `int` | The most rows "select all" and a Shift+click range select. Default: `5000` |
| `OnItemSelected` | `EventCallback<TModel>` | Raised when a row becomes the single selection |
| `OnItemOpened` | `EventCallback<TModel>` | Raised when the user opens a row (open column, double click, Enter, the row menu's Edit) |

UI features:
- **Click selects, the first column opens** - see "Selecting and opening rows" below
- **Row menu** (right-click, the `⋯` button, Shift+F10 / the context-menu key) - see "The row menu" below. The menu
  acts on the row the pointer is over, which the list learns from cell focus — a data-grid row is a `tr` with
  `display: contents` and cannot be focused itself. The menu is clamped into the viewport: it flips
  above and to the left of its anchor when there is no room, and a menu taller than the viewport scrolls
  inside itself (`BaseListContextMenuPlacement`).
- **Per-row "more" button** (`⋯`) — a narrow (32 px) column directly right of the open column; opens the same menu
  from a button, for touch screens and narrow windows that have no right mouse button. Every row action is therefore
  reachable without a right-click.
- **Delete confirmation** names the row (its `[DisplayKey]`, a card display key, otherwise the first value the
  row shows) and uses the destructive presentation: warning icon, danger-styled button, focus on Cancel.
- **One Tab stop per grid** — the cell that has focus stays in the Tab order and the arrow keys move between
  cells, instead of every cell being a Tab stop.
- **Empty state** — with no items the grid is not rendered at all and only `EmptyText` (default:
  the framework's localized "no items found") is shown, so FluentUI's own untranslated message never
  appears beside it.
- **Boolean columns** render the framework's localized `BoolTrue`/`BoolFalse` ("Yes"/"No", "Ja"/"Nein"),
  the same wording the filter panel offers for the property — not the raw CLR `True`/`False`.
- **Cell template** — `<BaseColumn><Template Context="row">…</Template></BaseColumn>` or fluent
  `.Column(x => x.Status, c => c.Template(row => …))` renders the cell with a fragment instead of the
  value, for example the localized caption of a string-coded status. The phone tile uses it for a badge too.
- **Column width** — a column's `Width` (markup `Width="120px"` or fluent `.Width("120px")`) is any
  valid CSS grid track. Columns left without one get a width that suits what they hold: a floor per
  data type (yes/no narrow, numbers narrow, date and time and enums wider, e-mail addresses -
  `[EmailAddress]`, an `Email`/`Mail`/`UserPrincipalName` property name - and long text widest, names in
  between), raised where the header title would not fit on two lines, plus a share of the free space in
  proportion. Plain text columns never shrink below `--base-list-column-min-width` (default `10rem`). A
  narrow viewport scrolls the grid sideways inside its container, and a soft shadow on the sticky "more"
  column shows that more columns lie beyond it. A cell clips what does not fit with an ellipsis and
  carries the full text as a tooltip; a header wraps to a second line before it is cut, and carries the
  column description (`<Property>_Tooltip`) as a tooltip on its text while the sort button keeps the
  column title as its accessible name.
- **Date and time columns** show the short date and time (the date alone at midnight), in the viewer's
  local time when the stored value is a UTC instant: a `DateTime` of kind `Utc`, a `DateTimeOffset`, the
  audit fields `CreatedOn`/`ModifiedOn` of `AuditModel` (UTC by convention, but without a kind once read
  from a database or JSON), or any property marked `[UtcDateTime]`. Any other value is shown as stored,
  which is what a date-only value or a server-local timestamp needs. "Local" is the time zone of the
  runtime, i.e. the browser's under WebAssembly - the same as the notification bell.
- **Multi-select** (Ctrl/Shift+click, keyboard) + select circles in multi-select mode
- **Bulk delete** with confirmation
- **Per-column** `Sortable` / `Filterable` (default: both `true`)
- **Role-based column visibility** via `[CrudAccess]` on the entity or `.Access(roles, rights)` on the column builder

#### Virtualization and paging

The list is virtualized **and** the page scrolls: there is one vertical scrollbar, the one of the layout's main
area, and the sticky header stays on screen. Only the rows near the visible part of the page exist in the DOM
(about 30 to 45, whatever the length of the list), so a list of 100,000 rows costs what one of 100 rows costs.
The status text beside the actions ("100,000 entries") is the total the data provider reported; it is shown
with the table and left out on the smartphone tile layout.

**One first query.** A list sends its first query once, when it opens. `RefreshAsync()` called before that first load
is done (a page that refreshes its list from its own first render, for example to poll) is ignored: the first load
fetches the current data anyway.

**How rows are fetched.** The grid asks for the rows it is about to show; the list answers from a cache of fixed
chunks of `PageSize` rows (default 50, kept between 10 and `BaseQueryLimits.MaxPageSize`) and fetches the
chunks it misses from the data provider with `BaseQuery.Skip`/`Take`, filters, sorts, select and navigation
filters, aligned to chunk boundaries. A chunk is fetched once however often it scrolls into view, neighbouring
chunks of one request are fetched together, two requests for the same chunk share one answer, the chunk after
the visible one (in the direction of scrolling) is read ahead, and a request the grid cancelled because the
user scrolled on never reaches the provider (the grid waits 100 ms before it asks). The cache holds at most
5,000 rows and drops the least recently used chunks. A reload after a delete, a create or `RefreshAsync()` loads
the chunks that are on screen again, so the user keeps their place and the focus (`holdFocus` in
`baseListInterop.js` puts the keyboard back on the equivalent cell when a virtualized row is replaced).

A data provider that ignores `Skip` and `Take` and hands back the whole list as one page
(`InMemoryBaseDataProvider`) is recognised by its answer (more rows than the request could have matched) and
its list is then served by index without further requests. Every data provider must report the total of the
matching rows (`BaseQueryResult.TotalCount`) with each answer: the scrollbar is sized by it.

**What the layout has to provide.** The virtualizer (ASP.NET `Virtualize`, used by `FluentDataGrid` and by the
tile view) observes the nearest ancestor of its spacer rows whose computed `overflow-y` is anything but
`visible` - `hidden` and `clip` count - and the browser viewport when there is none. A list is therefore fine
inside any page whose scrolling element is the layout's main area, and it must not sit inside an element with its
own `overflow: auto|hidden|clip`. The grid itself is such an element while it scrolls sideways, which is why
`.base-list-grid` is `overflow: visible` ("fits") by default and only `baseListInterop.js` switches it to
"scroll" (a wider table inside its own scroll box, with the pinned `.base-list-hscroll` scrollbar) once the
virtualizer is running. Rows have a fixed height (`ItemSize` 48 px, `--base-list-row-height`); a cell clips what
does not fit, so no row grows. A FluentUI row is `display: contents` and therefore has no box for the virtualizer to
measure, so the rows are subgrid rows (`BaseList.razor.css`).

**Limits.** Browsers cap the height of an element: Chromium clamps it at 33,554,432 px (2^25, measured), Firefox at
about 17.9 million px (documented, not measured here). A table of rows of 48 px therefore reaches about 690,000
rows in Chromium (650,000 rows were verified, 750,000 rows broke the list: the rows stay placeholders and the
scroll position jumps) and about 370,000 in Firefox, tiles of about 210 px about 160,000 and 85,000; beyond that the end
of the list is unreachable by scrolling, so use filters (a list of that size is a reporting problem, not a browsing
one).

**Selection.** Rows are selected by `Id`, not by instance, so a selected row stays selected when it scrolls out
of the cache and is fetched again. "Select all" selects the rows that are currently held in the cache (up to 5,000),
not the whole filtered result - provisional behaviour, whether it should mean the whole result is an open design
question.

Breaking changes against the load-more list: `BaseList.LoadMoreAsync()`, the **Load more** button and the
infinite-scroll hook (`OnGridNearEndAsync`, `updateGrid(grid, hasMore)`) are gone, as are the resource strings
`LoadMore` and `LoadedOfTotal` (`TotalEntries` replaces them), and `BaseListCardViewModel.Items` is replaced by
`ItemsProvider`, `TotalCount`, `DataVersion` and `TileHeight`.

#### Selecting and opening rows

A click **selects**, it no longer opens the card. The mouse works like in Business Central:

| Gesture | Effect |
|---|---|
| Click on a row | The row becomes the only selected one (and the anchor of a range). `OnItemSelected` fires when it becomes the single selection |
| Ctrl+click / Cmd+click | Toggles the row; the anchor moves to it |
| Shift+click | Selects every row from the anchor to the clicked row, by position. Rows that are not loaded (the list is virtualized, a range can span many chunks of a 100,000-row list) are read from the data provider in pages of `BaseQueryLimits.MaxPageSize`, not through the chunk cache. A range longer than `SelectAllLimit` keeps the rows nearest the anchor and a message above the list says so |
| Click on the open column | Selects the row and **opens** it (not with Ctrl or Shift held) |
| Double click on a row | Opens it |
| Right-click on a row outside the selection | Selects that row first, then opens the row menu |

Selected rows carry the row class `base-list-row-selected` (an accent tint from `--color-bg-selected`, distinct from the hover
fill, light and dark); a row blocked by a toolbar action (see action scope) carries `base-list-row-blocked` as well, a row can
have both. While Shift is held the rows cannot be selected as text, so a range does not highlight text.

**The open column.** The first visible column that is not the tile's action row (`MobileRole.Actions`) is the open link:
its cell has the class `base-list-open-cell`, takes the accent color and an underline while the pointer is on the row (touch
screens have no hover) and on the selected row, and a click on it opens the card. The link color is `--color-accent-text`
(the accent text color, at least 4.5:1 on the surface, light and dark); it is set in the list's own `--base-list-open-link` variable (the part: `--base-listpart-open-link`).
Mark another column with
`<BaseColumn OpensItem="true" />` (fluent: `.OpensItem()`), or switch the link off with `OpenColumn="false"`.
A template column with its own buttons keeps working: a button that stops the click's propagation neither selects nor opens.

**Opening.** Opening shows the edit card when the user may edit (`AllowEdit` and the modify right). A user who may only read
gets the card read-only when `AllowView` is on (default): `BaseDialogData.ReadOnly` is passed to `BaseCard`, which shows every
field as a value, has no save or cancel buttons, no way to add, remove or reorder list-part items, and is titled
"View - <display key>" ("Anzeigen - ..."). A list without a `CardConfiguration` and without a `CardType` shows a card built from its
visible columns (see **The card of a list without a card definition** below); a custom `CardType` is only opened read-only when it
declares a `ReadOnly` parameter, which the
dialog sets. Opening a row by a deep link (`?item=`) honors the same rule. The list parts of a read-only card cannot add, delete,
reorder or edit: `BaseCard` hands its `ReadOnly` to every `BaseListPart`, whose item dialog then opens read-only too.

**The card of a list without a card definition.** When neither `CardConfiguration` nor `CardType` is set, the list builds the card
from its own columns each time it opens one (for "View", for Edit and for Add): one field per column the current user may see
(`[CrudAccess]` and the column's access rules, hidden columns and the tile's action row excluded), in column order, with the
column's title and tooltip as label and tooltip. A property that is not a column is not shown. A list with an explicit
`CardConfiguration` keeps using it unchanged. The fields show the values the way the list does: a column with a `Format`, a
date or time column and a column that reads through a navigation (`item.Category.Name`) show the text of the cell in a read-only
field, a column with a `Template` draws that template, and everything else (text, numbers, yes/no switch, enum, a reference
navigation) uses the normal field of the card. A text of more than 35 characters (or with several lines) gets a full-width
multi-line field sized to the text (at most eight lines), so a long value is never cut off on a narrow screen. The card title is built from the `[DisplayKey]` properties, otherwise from the open column. Opened read-only the
card has no save button and nothing to edit. Opened for editing, the fields of properties with a public setter are editable, but
not the key (`Id` or `[Key]`; a string key is editable on a new item), a column with a template, or a column that reads
through a navigation. Set a `CardConfiguration` for a list that needs more than its columns.

**Row identity.** The list recognises a row again after a reload (the selection, the highlight, the row menu and the range
cache keep working when every row instance is replaced) by its key: the `Id` property, otherwise the single property marked
`[Key]` (a model keyed by `Name` works). A model with neither, a composite key (several `[Key]` properties) or an unset key
is compared by reference, so after a reload the selection stays but its row loses the highlight; give such a model an `Id` or a
`[Key]` property. The same rule applies to `BaseListPart`.

**Multi-select mode.** The round select circles are shown while two or more rows are selected or while `MultiSelectMode` is on
(`BaseList.EnterMultiSelectMode()`, internal; the row menu's "Select more" entry sets it). Escape and "Deselect all" leave it. Multiple
rows can only be selected where something consumes a selection (a delete right or a declared item action), otherwise Ctrl and
Shift behave like a plain click.

#### The row menu

One menu serves the right-click, the `⋯` button and the keyboard (Shift+F10 or the context-menu key on a focused cell; it opens
beside the row's button). A right-click or a click on the button selects the row first when it is not part of the selection; a
row that is part of a multi-selection keeps it. Entries appear only where they apply, in this order (Business Central):

1. **View** (the list is not editable, but `AllowView` and the read right apply: opens the read-only card) or **Edit** (opens
   the card, raises `OnItemOpened`).
2. **Delete** - names the count ("Delete 3") when the row is part of a multi-selection; the confirmation is unchanged.
3. The declared item-bound actions (`CrudActionContext.ContextMenu`, see action scope), one block per action group, then the
   legacy `ContextMenuActions` render fragment (kept for other sites; prefer declared actions) after a divider.
4. A divider, then **Select more** (shows the select circles and keeps the clicked row selected; hidden while they are already
   shown), **Select all** and **Deselect all** (only while something is selected or the select circles are shown). A list that
   cannot consume a selection (no delete right, no item action) has none of these.

Dividers sit only between non-empty sections. Edit/View and Delete live only in this menu; the toolbar has no buttons for them.

**The `⋯` button** is a column of its own (`base-list-row-menu-column`, 32 px) directly right of the open column: with select
circles the order is select circle, open column, `⋯`, the other columns. The old sticky last column is gone. The button is
invisible by default but keeps its width, so showing it moves nothing: it appears on the hovered row, on the selected row, while
the row holds the keyboard focus (`:focus-within`), while its menu is open (row class `base-list-row-menu-open`) and always on
screens without hover (`@media (hover: none)`). It is not a Tab stop (`tabindex="-1"`); the keyboard opens the menu with
Shift+F10 or the context-menu key on a focused cell. The hovered button uses `--base-list-control-fill-hover`, the fill of the
header controls. A list without any row-menu entries gets no button column.

**Keyboard** (focus on a data cell of the grid). The *current row* is the row that carries the keyboard focus; the arrow keys move
the focus themselves (the script moves it at once and tells the list which row it is on), so the focus, the selection's last row and
the row Enter opens are always the same row, also while the virtualized grid is scrolled or re-rendered. The focus rests on the open
cell of the row and shows the browser's focus ring there.

| Key | Effect |
|---|---|
| Up / Down | Outside multi-select mode: moves the focus and replaces the selection with the new current row. In multi-select mode (select circles shown, either through `MultiSelectMode` or two or more selected rows): moves only the focus, the selection stays |
| Ctrl+Up / Ctrl+Down | Always moves only the focus (the selection stays) |
| Shift+Up / Shift+Down | Extends the selection from the anchor (this puts the list into multi-select mode when two rows are selected) |
| Space | Toggles the current row in multi-select mode, otherwise selects it |
| Enter | Opens the current row |
| Ctrl+A | Selects all (see below) |
| Escape | Clears the selection and leaves multi-select mode. Only while the focus is on a grid cell: Escape in a header control or in a dropdown (for example the quick filter) belongs to that control |

The script reports a key to `OnGridKeyPressedAsync(key, shift, ctrl, row, target)` (`row`: the row the focus stood on, `target`: the
row an arrow key already moved the focus to) and moves the focus to the row it returns when that differs (a row the virtualizer has not
rendered yet is scrolled to). Closing the row menu (Escape, click outside) or choosing one of its entries that keeps the list on
screen (Select more, Select all, Deselect all) puts the keyboard back on the open cell of the row the menu was opened for; entries that
open a card or a confirmation manage their own focus. The quick filter dropdown raises its change only when the chosen entry differs
from the one shown, so closing it with Escape leaves the row selection alone.

**Select all** takes every row of the current filter, not just the loaded ones: it reads them from the data provider in pages of
`BaseQueryLimits.MaxPageSize`, up to `SelectAllLimit` (default 5000). Above the limit the first rows are selected and a localized
message (`SelectionLimitReached`) says how many of how many; nothing is truncated silently.

**Selection lifetime.** A new filter, quick filter or sort (every reload that starts at the top) clears the selection. A refresh
after an action or a delete keeps the selected rows that still exist: the loaded ones are replaced by their fresh data, rows that are
not loaded stay as they were; a deleted row leaves the selection.

#### Editing a row: the card, deep link and single-row refresh

Opening a row (see above) shows the entry in a `BaseDialog` (the card). With `EnableDeepLinking` (default
`true`) the dialog is tied to the URL: opening a row navigates to the same page with `?item=<id>` (the name is
`DeepLinkParameterName`, default `item`), so the card can be bookmarked, shared and reached with the browser's back
and forward buttons. A list without an `Id` property, or with `EnableDeepLinking="false"`, opens the card without
touching the URL and is not affected by URL changes.

**Unsaved changes survive a navigation.** While a deep-linked card is open, the list holds every navigation that
would remove or change the `item` parameter or leave the page - the browser's back and forward buttons, the back
gesture of a phone, a link in the side navigation - when the card has unsaved changes. It asks the card to close the
way the X button and Escape do (`BaseCard`'s confirmation: "Discard changes?"):

- **Discard** closes the card, drops the edits and carries out the navigation that was held (back ends on the list
  without the parameter; a link goes to its target, replacing the card's history entry).
- **Keep editing** keeps the card open and the URL as it was (`?item=` stays).

A card without unsaved changes closes without a question, as before. The mechanism is
`NavigationManager.RegisterLocationChangingHandler`, registered only while a deep-linked card is open and released
when it closes or the list is disposed; the card reports its state through an internal `CardCloseGuard` that
`BaseDialog` cascades to the card it hosts, so a custom `CardType` that renders a `BaseCard` takes part without
doing anything. A navigation manager that cannot lock navigation (the handler registration throws
`NotSupportedException`) falls back to closing the card without asking. Closing the browser tab or reloading the
page is not covered.

**After a save only the edited row is read again.** When the card closes with a saved entry, the list does not
reload its visible window. It reads that one entry with `GetByIdAsync(id, select)` (the select list is the one the
list queries with: the visible columns, the display keys and the properties the tiles need), replaces the row in
the cache by its `Id` and re-reads the grid or the tile view from the cache, so the scroll position, the focus and
the selection stay. One `PATCH` and one `GET` by id are all the browser sends. Provisional behavior: the row stays
at its position even when the new values would sort it elsewhere or no longer match the filter, until the next
reload, sort or filter change. Falls back to the reload of the visible window (`RefreshAsync()`) when the entry is
not held by the list right now (not loaded yet, evicted from the cache), when the provider no longer finds it
(deleted meanwhile, no longer readable) or when a load is under way; when the provider throws, the saved model the
card returned is shown instead. **Creating and deleting always reload the visible window.**

A host that changes an entry outside the card (a custom action, its own dialog) can do the same:

```razor
<BaseList @ref="ProductList" TModel="Product" ... />

@code {
    private BaseList<Product>? ProductList;

    private async Task OnPriceRaisedAsync(Guid productId)
    {
        await ProductService.RaisePriceAsync(productId);
        await ProductList!.RefreshItemAsync(productId);
    }
}
```

`public Task RefreshItemAsync(object id)` takes the value of the entry's `Id` property.

#### Order and paging

Rows never come back in an arbitrary order. The list asks for, in order of precedence: `DefaultSorts` on the
list, `BaseListConfiguration.DefaultSorts` (`BaseListBuilder.DefaultSort(x => x.CreatedOn, descending: true)`),
the entity's `[DefaultSort(nameof(CreatedOn), Descending = true)]` attributes. Whatever was asked for,
`DbContextBaseDataProvider` appends the primary key as the last tie-breaker (and keeps an order the query
customizer imposed when none was asked for), so `Skip`/`Take` pages are deterministic.

Clicking a sortable column header replaces that order: the sort goes to the data provider as the query's
`Sorts`, the rows are loaded again in the new order from the top, so the top rows are the true top rows of the
whole data set. The database, the HTTP endpoint and the in-memory provider all sort every row; a second click
reverses the direction.

#### Smartphone tiles

A wide table that scrolls sideways is a poor phone experience, so below a container width a list shows **one
tile per item** instead of the table. Everything else stays: virtualized scrolling, sorting,
filtering, the add button, row click (the card opens, deep link included), the context menu, selection and the
row actions.

**Switching.** A `ResizeObserver` (`baseListMobile.js`) watches the width of the list itself - the container,
not the window, so a list in a narrow side panel of a wide screen switches too - and tells the list when it
crosses `MobileBreakpoint` (default 700 px). Only one of the two layouts is ever rendered, so the virtualized
grid is not built while tiles are shown and the other way round; the first answer arrives before the first page is
loaded, so there is no flash of the wrong layout. `MobileLayout = Always` / `Never` bypasses the script. The
root element carries `base-list-mobile` while tiles are shown, so a page can react in CSS
(`.page:has(.base-list-mobile) .scroll-hint { display: none }`).

**The tile.** A title, an optional subtitle, badges (chips), up to a few label and value pairs, an optional action
row and a trailing **more button** (44 x 44 px) that opens the same menu as the table row. Every tile has **the same height**, worked out from the
parts the plan puts into it (`BaseListTileMetrics`): the title takes up to two lines, the subtitle, the badge row
and each value one line (longer text is cut with an ellipsis, the full text is in the card), the fields are laid out
two to a row. The tiles are virtualized, and ASP.NET's `Virtualize` positions the tiles it does not render by the
average height of the ones it does: with tiles of different heights that average changes whenever another set is on
screen, and with tens of thousands of tiles above the visible ones a single pixel moves the content by thousands. A
tile with a custom body (`MobileTemplate`) is 120 px high unless `MobileItemHeight` says otherwise; what does not
fit is cut off. Every tile has a **three-dot button** in its top-right corner (always visible, 44 px, inside the tile's
fixed height, so it does not change the computed height); see "Phone interaction" below for tap, the menu, long press
and the selection bar. **Every part the plan has is always
rendered**, so the room reserved for it is used and the tile looks full instead of having a hole: an empty subtitle
or field value shows a muted dash (`–`, announced as "No value"), a boolean badge always shows its state (true: the
accent chip with a check mark, false: a muted chip with a dismiss mark and the same label, `BoolTrue`/`BoolFalse`
as the accessible state), a badge with an empty text is left out and the badge row shows a muted dash when nothing is
left in it. The action row is reserved only when the plan has action columns or the tile configuration has actions.
What a one-line title leaves over (the title reserves two lines) sits as one small gap at the bottom of the tile. The
entry count ("12 entries") of the list header is not shown on the tile layout, only with the table. Above the tiles a sticky **sort bar** offers the
sortable columns plus "Default order" and a direction button; like a header click it sorts on the data provider,
not on the loaded page. The filter button of the list header opens the filter panel as usual.

**Phone interaction.** A phone has no hover, no double click, no Shift or Ctrl key and no right mouse button, so the
tile list does the Business Central list interaction with touch gestures:

| Gesture | What happens |
|---|---|
| Tap a tile | Opens it like a click on the open column: the edit card, or the read-only card ("View") when the user may only read and `AllowView` / the card allows it; raises `OnItemOpened` (and `OnItemSelected`, as before). Nothing opens when the user can open nothing. While rows are being picked a tap toggles the tile instead |
| Three-dot button of a tile | Opens the same row menu as the table: View/Edit, Delete (with the count when the tile is part of a multi-selection), the declared item-bound actions (a disabled action shows its **reason as text** below the caption, because there is no tooltip on a touch screen), a divider, Select more, Select all, Deselect all. The menu is a **bottom sheet**: full width, at the bottom edge of the screen (above the safe area), dimmed backdrop, so it is inside the viewport at any width (320-390 px and wider) |
| Long press (about 500 ms) | Starts **multi-select mode** with that tile selected (`EnterMultiSelectMode()`); the select circle appears on every tile. The finger may not move more than 10 px and the page may not scroll, so a swipe never selects; the tile does not start a text selection or the system callout, and the browser's own context menu for a long press is suppressed (the three-dot button is the menu). The release does not also open the tile. Only where a selection leads somewhere (a delete right or an item-bound action) |
| "Select more" / "Select all" in the menu | Also enter multi-select mode |
| Tap in multi-select mode | Toggles the tile. Taking out the last selected tile leaves the mode |
| "Deselect all" (menu or bar), the browser's/phone's **Back** | Leave the mode and clear the selection. Back is intercepted only while the mode is on and only for a navigation that is not a link of the page, so the links of the app still work |

**The selection bar.** In multi-select mode a bar sticks to the bottom of the list's scroll area with a close button
("Deselect all"), the count ("3 selected"), the **declared item-bound toolbar actions** (the same enabled state and
reasons as the table's toolbar: a single-row action is disabled with several tiles selected, a multi-row action is
disabled when a selected row fails its `Enabled` predicate; tapping a disabled action shows its reason in the bar; the
actions collapse into a "more actions" button when they do not fit the width) and a **Delete** button for the whole
selection when the user may delete. The scroll area ends above the app's bottom navigation bar, so the bar never covers
it; without such a bar the safe-area inset of the screen is added underneath. A tile that blocks the action under the
finger is marked with a warning bar on its left edge, a selected tile has an accent border and tint (theme tokens, light
and dark). The **item-independent** toolbar actions (`CrudActionScope.None`) stay in the list header (collapsed into the
"more actions" menu when the header is too narrow); the item-bound ones are not repeated there on the tile layout.
`BaseActionToolbar.ItemFilter` (`ToolbarActionFilter.All` / `ItemBound` / `ItemIndependent`) and `OnReasonRequested` are
what the list uses for this split; they are public for other toolbars.

**Legacy: `MobileRole.Actions` columns.** A column with the role `Actions` still becomes the tile's action row (see
"Row action buttons"), so sites that have not migrated keep their buttons. New lists declare the actions as
`CrudAction`s instead: they are reachable through the three-dot menu and the selection bar without a button column, and
the action row can then be dropped.

**What the tile shows** - four ways, strongest first:

1. The list's tile configuration (fluent builder, or the `MobileCard` parameter):

```csharp
var list = new BaseListBuilder<User>()
    .Column(u => u.Email).Column(u => u.UserName).Column(u => u.IsInactive).Column(u => u.CreatedOn)
    .MobileCard(card => card
        .Title(u => u.Email)
        .Subtitle(u => u.UserName)
        .Badge(u => u.IsInactive)               // a chip with the column title, showing the state (check / dismiss mark)
        .Field(u => u.CreatedOn)                // label and value; add "Label" as 2nd argument to override the label
        .Field(u => u.NumberOfSessionsCreated)  // a property without a list column is read from the item
        .MaxFields(3))                          // optional cap, also for explicit declarations
    .MobileBreakpoint(600)
    .Build();
```

2. A role on the column - in markup `<BaseColumn Property="..." MobileRole="MobileRole.Title" />` (with an
   optional `MobilePriority="2"` to order columns of the same role), in the builder
   `.Column(u => u.Email, c => c.Mobile(MobileRole.Title))`.
3. A role on the entity property: `[MobileVisible(Role = MobileRole.Title)]`, `[MobileVisible(Order = 1)]`
   (a plain field), `[MobileVisible(Role = MobileRole.Hidden)]`.
4. **Automatic fallback** when nothing declares a title, subtitle, field or badge: the title is the first text
   column, the subtitle the second, up to two boolean or enum columns become badges, and at most four more columns
   follow - text before dates before numbers. Raw identifiers (`Id`, `...Id`), `Guid`s, binary data, collections
   and untemplated objects are never chosen; a column whose template shows a foreign key as a name is.

As soon as any source declares a title, subtitle, field or badge, the tile shows exactly the declared columns
(plus action columns); undecided columns are ignored. A tile without a title column falls back to the first
undecided text column, then to the item's display key.

`MobileRole` values: `Auto`, `Hidden`, `Title`, `Subtitle`, `Field`, `Badge`, `Actions`.

**Row action buttons (legacy).** A column with the role `Actions` is not a label and value pair: its template becomes the
tile's action row (Accept / Reject, Run now, Apply ...), with 44 px buttons. An unsortable column with a template
bound to `Id` is taken for such a column without a role. `MobileCardBuilder.Actions(item => ...)` adds a row
without a column. Whatever else the context menu offers (`ContextMenuActions`) is behind the more button.

**Full control.** `MobileTemplate` (or `MobileCardBuilder.Template`) replaces the body of the tile; the tile keeps
its frame (tap, more button, selection mark, action row):

```razor
<BaseList TModel="Order" MobileLayout="BaseListMobileLayout.Auto">
    <MobileTemplate Context="order"><strong>@order.Number</strong> <span>@order.Customer</span></MobileTemplate>
    <ChildContent>...</ChildContent>
</BaseList>
```

Notes: the tile list is virtualized and scrolls with the page, like the table, so a list of any length costs the same
(see "Virtualization and paging"). A sort picked on the tile list is dropped again when the list switches back to the
table (the table
starts from its header state). All strings are localized (`SortBy`, `SortDefault`, `SortAscending`,
`SortDescending` in `BlazorBaseCrudResources`). The pieces are public and testable on their own:
`MobileCardPlanResolver.Resolve(...)` computes the plan, `BaseListCardView<TModel>` draws it.

#### Filter panel fields

The filter panel offers the scalar properties of the entity that the user may read, except the technical
ones: the primary key (`Id`, `[Key]`) and the foreign keys that only reference another row (`[ForeignKey]`,
or `<Navigation>Id` next to a reference navigation). Naming a technical property explicitly in the filter
configuration (`Include`, `Field`) still offers it. Enum members are listed with the same display text the
cards use (`EnumLabelResolver`), while the filter itself keeps comparing by member name.

#### Saved filters, presets and defaults

The filter drawer has a **Saved filters** section (hidden when there is nothing to show) with two parts:

- **Presets** — named filters a developer declares in code and offers to every user ("Only pending").
- **My filters** — filters the user saved under a name: load, save (new / overwrite / rename), delete and mark one as
  the **default** (star). Saved filters are stored per user, so they follow the user across browsers and devices.

Choosing a preset or a saved filter applies it immediately and closes the drawer; the drawer's *Reset* clears the
active filter.

**Quick filter dropdown.** As soon as a list has at least one preset or saved filter, a dropdown appears right next to
the *Filter* button. It shows the active choice ("No filter", a preset, a saved filter, or "Custom filter" when the
active filter matches none of them) and switches between them, or back to unfiltered, without opening the drawer. It
refreshes whenever the drawer closes, so filters saved or deleted there show up immediately.

On the first load of a list the filter is resolved in this order:

1. the user's **saved default** for the list (an empty saved filter, for example "All", is a valid default and
   suppresses the code default),
2. the code default: the preset declared with `isDefault: true`, otherwise the unnamed `DefaultFilter`,
3. no filter.

A stored filter that names a field the list no longer offers, an operator the field no longer allows, or has
incomplete conditions is pruned (`FilterModelPruner`) before it is applied, so an outdated filter cannot break the
query. A store that cannot be reached falls back to the code default instead of failing the list.

**Declaring presets and defaults** (all in `BlazorBase.CRUD.SavedFilters`):

```csharp
// Fluent: BaseListBuilder
var config = new BaseListBuilder<Order>()
    .Column(o => o.Number)
    .Column(o => o.Status)
    .FilterPreset("pending", "Only pending", f => f.Is(o => o.Status, OrderStatus.Pending), isDefault: true)
    .FilterPreset("active", "Only active", f => f.Is(o => o.IsActive, true))
    .FilterPreset("big-open", "Large open orders", f => f
        .Is(o => o.Status, OrderStatus.Pending)
        .Where(o => o.Total, FilterOperator.GreaterThan, 1000m))
    .Build();
```

```razor
@* Markup: one parameter on the list *@
<BaseList TModel="Order" FilterDefaults="@FilterDefaults"> ... </BaseList>

@code {
    private static ListFilterDefaults FilterDefaults { get; } = new ListFilterDefaultsBuilder<Order>()
        .Preset("pending", "Only pending", f => f.Is(o => o.Status, OrderStatus.Pending))
        .Build();
}
```

| Member | Purpose |
|---|---|
| `FilterPreset(id, name, configure, isDefault = false)` / `ListFilterDefaultsBuilder.Preset(...)` | Offers a named filter. `id` is unique per list and stable; `isDefault` applies it on first load (only one per list). |
| `DefaultFilter(configure)` | An unnamed default applied on first load, not offered as a preset. |
| `FilterListKey(key)` / `ListKey(key)` | Storage key of the users' saved filters. Default: `<model full name>@<page route>` (see "List keys" below). An explicit key always wins unchanged. |
| `FilterGroupBuilder<T>` | `Is(x => x.P, value)`, `Where(x => x.P, FilterOperator.…, value)`, `Or()`, `Group(g => …)`; values are written as the drawer writes them (enum member name, `true`/`false`, `yyyy-MM-dd`). |

Preset names are localizable: the display text is looked up as `FilterPreset_<id>` in the model's resources
(`<Model>.resx` / `.de.resx`, the usual model-near localization) and falls back to the `name` passed in code.

**Deep link a preset.** A list route answers to `?preset=<id>`: `/BudgetBoosts?preset=my-pending` opens the list with
that preset applied. This is what a dashboard tile, a menu entry, a mail or a shared link uses to open "exactly the
rows the number counted".

- It applies on the **first load** of the list and takes precedence over the user's saved default and the code default
  **for this visit only**; it is never stored as the user's default. The quick filter dropdown shows the preset as the
  active choice, and the user can switch away from it like from any other filter.
- The id is the `FilterPreset.Id` and is matched case-insensitively. An **unknown id is ignored silently** and the
  normal default applies. A preset without conditions ("All") is valid and opens the list unfiltered, even when a
  default is declared.
- It works together with the card deep link: `/BudgetBoosts?item=<id>&preset=my-pending` filters the list behind the
  card (the order of the parameters does not matter), and opening or closing a card keeps the `preset` parameter.
- Only lists with `EnableDeepLinking` (the default) read it; a list in a dialog or a `BaseListPart` ignores the URL.
  `BaseList.PresetParameterName` (default `preset`) renames the parameter.

Build such links with `ListPresetLink.Build` (`BlazorBase.CRUD.SavedFilters`, pure and WASM-safe) instead of
concatenating strings; it keeps an existing query string, replaces an existing `preset` parameter, keeps a fragment at
the end and URL-encodes the id:

```csharp
ListPresetLink.Build("/BudgetBoosts", "my-pending");        // "/BudgetBoosts?preset=my-pending"
ListPresetLink.Build("/Users?team=7", "no-team");           // "/Users?team=7&preset=no-team"
ListPresetLink.Build("/Jobs?preset=old#top", "failed");     // "/Jobs?preset=failed#top"
```

**List keys.** Saved filters, the saved default and the "My filters" list belong to a *list key*. By default the key is
`<model full name>@<page route>`, for example `Dev.Acadon.Shared.Modules.Users.Models.User@/users`, so two pages that list
the same model (a "Users" and an "Enrolled users" page) never share filters or defaults. The route is the page path
from `NavigationManager`, relative to the base address (so a PWA base href such as `/app/` is not part of it), without
query string and fragment, lower-cased and trimmed of slashes (`/` for the root). The whole key is limited to 256
characters (`ListFilterValidator.MaxListKeyLength`); a longer route is cut and ends in `~` plus a 16-digit hash of the
full route, so the result stays deterministic and distinct (`SavedFilterKey.Resolve`).

An explicit `FilterListKey(...)` / `ListFilterDefaults.ListKey` always wins and is used unchanged. Set one for lists
that do not own a page: a list inside a dialog, a `BaseListPart` or any component placed on a page shares that page's
route, and the same list shown on several routes (or under route parameters such as `/orders/{Id}`) would otherwise get
one key per route. Saved filters created before this key format existed were stored under the plain model name and are
not offered under the new key; give the list `FilterListKey("<model full name>")` to keep using them.

**Storing user filters.** The list resolves an optional `IListFilterStore` from DI (no store: presets and code defaults
still work, the "My filters" part is hidden). Register one:

| Host | Registration | Storage |
|---|---|---|
| WASM / MAUI with a server | `services.AddBlazorBaseListFiltersClient(c => c.BaseAddress = new Uri(api, "api/list-filters/"), b => b.AddHttpMessageHandler<…>())` | Server, per user (`HttpListFilterStore`) |
| Server | `services.AddBlazorBaseListFilters<TContext>()` + `app.MapBlazorBaseListFilters()` + `modelBuilder.ApplySavedListFilterConfiguration<TUser>()` in `OnModelCreating` (then add an EF migration) | EF Core table `SavedListFilters` (`EfListFilterRepository<TContext>`) |
| No server storage | `services.AddBlazorBaseListFiltersLocalStorage()` or `AddBlazorBaseListFiltersInMemory()` | Browser `localStorage` (per browser profile, not per user) / process memory |

`TContext` must be registered with `AddDbContextFactory<TContext>`. Server-only types
(`BlazorBase.CRUD.SavedFilters.Server`: `SavedListFilter`, `IListFilterRepository`, `EfListFilterRepository`,
`MapBlazorBaseListFilters`, `AddBlazorBaseListFilters`, `ApplySavedListFilterConfiguration`) are never referenced by
the client-side code path; the client only uses `IListFilterStore`, `HttpListFilterStore` and the DTOs.

`IListFilterStore` (`GetAsync`, `SaveAsync`, `DeleteAsync`, `SetDefaultAsync`, all keyed by list key) has the
semantics: `SaveAsync` with an `Id` renames/overwrites that filter, without one it overwrites the filter of the same
name (case-insensitive) or creates one; renaming onto another filter's name throws `ListFilterNameConflictException`;
an unknown id throws `KeyNotFoundException`; `SetAsDefault` / `SetDefaultAsync` keep exactly one default per list.

**Endpoints** (`MapBlazorBaseListFilters(route = "api/list-filters")`, every one `RequireAuthorization()`):

| Verb and path | Purpose | Result |
|---|---|---|
| `GET {route}?listKey=` | The caller's filters for the list | `200` list |
| `POST {route}?listKey=` body `SavedListFilterInput` | Create / overwrite / rename | `200` filter, `400` invalid, `404` unknown id, `409` name taken |
| `PUT {route}/default?listKey=` body `{ "id": guid? }` | Set or clear the default | `204`, `404` unknown id |
| `DELETE {route}/{id}?listKey=` | Delete | `204`, `404` |

The user is taken from the authenticated principal (`NameIdentifier`, else `sub`), never from the request; every
query is restricted to that user, so another user's filters are neither listed nor reachable by id (`404`, not
`403`). Limits (`ListFilterValidator`): name ≤ 100 characters, list key ≤ 256, ≤ 100 filters per user and list,
filter depth ≤ 6, ≤ 100 conditions, value ≤ 1000 characters. Mutating calls are subject to the host's CSRF
protection like every other cookie-authenticated API call, so register the client with the same message handler as
the CRUD client.

**Deleting users.** The filters of a deleted user must not stay behind. Two options:

1. *Foreign key (recommended when the host has a user table with a string key, such as ASP.NET Core Identity).*
   Configure the model with the generic overload and add an EF migration:

   ```csharp
   protected override void OnModelCreating(ModelBuilder modelBuilder)
   {
       base.OnModelCreating(modelBuilder);
       modelBuilder.ApplySavedListFilterConfiguration<AppUser>();
   }
   ```

   This adds `SavedListFilters.UserId` → the key of `AppUser` with `ON DELETE CASCADE`: deleting the user row deletes
   their filters in the same statement. The migration that adds the key to an existing table must first delete rows
   whose `UserId` has no user (`DELETE FROM [SavedListFilters] WHERE [UserId] NOT IN (SELECT [Id] FROM [AspNetUsers])`),
   otherwise adding the constraint fails. A save for a user id that is not in the user table is rejected by the database.
2. *No foreign key (the non-generic `ApplySavedListFilterConfiguration()`, for hosts without a user table).* Call
   `IListFilterRepository.DeleteAllForUserAsync(userId)` from the host's user-deletion path; it removes the user's filters
   across all lists and returns how many it deleted.

Localized UI strings live in `SavedFilterResources.resx` / `.de.resx`.

### `BaseCard<TModel>`

Detail/edit form for a single entity. Generic constraint: `where TModel : class, new()`.

| Parameter | Type | Notes |
|---|---|---|
| `Model` | `TModel` | The bound entity |
| `Configuration` | `BaseCardConfiguration<TModel>?` | From `BaseCardBuilder` |
| `MaxColumns` | `int` | Responsive CSS grid |
| `DataProvider` | `IBaseDataProvider<TModel>?` | Optional |
| `IsNew` | `bool?` | Whether the model is being created. Unset, the key is inspected instead |
| `ReadOnly` | `bool` | Shows every field as a value, hides the save and cancel buttons and the list-part add/delete/reorder controls (the list parts are read-only too: their items open read-only); the title becomes "View - <display key>". `BaseList` sets it (via `BaseDialogData.ReadOnly`) when a user may only read |
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
- Catches **409 Conflict** and shows a friendly `FluentMessageBar` (dismissed with its own close button).
- Renders inputs via `BasePropertyInput` (typed automatically: string → `FluentTextInput`, bool → `FluentSwitch` with its label above like the other fields, enum → `FluentSelect<EnumOption,string>`, DateTime → `FluentDatePicker`, navigation → select or browse; numbers stay text inputs with the card's own parser and localized messages). Validation messages are the card's own (`base-field-errors` under the field); the inputs' built-in messages are off (`MessageCondition = FluentFieldCondition.Never`). A nullable enum property (`MyEnum?`) gets an extra blank "— Select —" option mapping to `null`; an existing `null` value shows that blank option instead of visually defaulting to the first enum member. Non-nullable enums are unaffected.
- In the read-only ("View") card the fields show values only, so a read-only field carries no required marker (the asterisk appears only while a field is editable).
- **A field that cannot be changed inside an editable card looks read-only at first sight.** When the card itself is
  editable (`ReadOnly` false) and a field is not editable for the user (`.Editable(false)`, no modify right on the
  property, a calculated value), its wrapper carries the class `base-property-input-readonly` and the attribute
  `data-readonly="true"`. The stylesheet then draws the text of the control muted (semantic tokens only; the muted text
  keeps WCAG AA contrast), for text, number, date, text area, select and switch alike, in the light and the dark theme.
  A card that is read-only as a whole (the "View" card) keeps its calm look: no marker, no muted
  controls. `BasePropertyInput` takes this from its `HighlightReadOnly` parameter, which `BaseCard` sets to
  `!ReadOnly`; custom property displays inherit the muted text colour from the wrapper. A field template gets the full
  width of its column with the class `base-field-control`.

### `CardDialogDefaults`

`BlazorBase.CRUD.Components.CardDialogDefaults` is the single source of truth for the shape of a dialog that hosts a
card. `BaseList` (card dialog) and `BaseListPart` (item dialog) open their dialogs with it; open your own card dialog
with the same shape through `ShowCardDialogAsync` (extension of `IDialogService`, namespace `BlazorBase.CRUD.Components`)
or `CardDialogDefaults.Create()`:

```csharp
var result = await DialogService.ShowCardDialogAsync(data);
var saved = result.Cancelled ? null : (TModel?)result.Value;
```

`Create()` returns a new `BaseDialogOptions` the caller may adjust: no title bar (the card renders its header), no
dismiss button, and Escape is handled by the card (`PreventDismissOnEscape`), which asks before it discards changes.
The dialog content is a `BaseDialogInstance` (see [BlazorBase.Components](BlazorBase.Components.md)); the dialog carries
the class `base-dialog`, and the card's `DialogBehavior` makes everything outside it inert. `BaseDialogData<TModel>.CloseAsync()`
closes the dialog from outside (for example when the list is disposed); the awaited result is then cancelled.
`ShowListPartItemDialogAsync` does the same for the item dialog of a list part.

The width is fluid, `CardDialogDefaults.Width` = `min(calc(100vw - 2rem), clamp(600px, 62vw, 1100px))`: 600 px on a
tablet, growing with the window to at most 1100 px on a wide desktop (a card then reaches its three-column layout and
needs less vertical scrolling), and never wider than the viewport minus 2 rem (a phone gets the full width). The number
of columns still comes from the card's own container width (`BaseCard.razor.css`), and a card higher than the window
scrolls inside the dialog. The parts of the formula are public constants (`MinimumWidth`, `PreferredWidth`,
`MaximumWidth`, `ViewportMargin`).

### `BaseListPart<TModel>`

Embedded child collection inside a `BaseCard`. Same generic constraint as `BaseCard`. It is a plain item list (not a data grid)
held in memory: the card saves the changes (adds, removes, edits) together with the parent. It follows the same interaction
model as `BaseList`, so the two feel alike.

| Parameter | Type | Notes |
|---|---|---|
| `Items` | `ICollection<TModel>` | The child collection. A different collection clears the selection; the same collection edited in place (items added or removed) keeps it for the items that are still there |
| `EditMode` | `ListPartEditMode` | `Dialog` (default; an item opens a deferred-save card), `Inline` (inline editors per item) or `None` |
| `AllowAdd`, `AllowDelete` | `bool` | Both are also limited by the insert and delete rights of the user |
| `ReadOnly` | `bool` | Nothing can be added, deleted or edited; an item opens in a read-only card ("View"). `BaseCard` sets it from its own `ReadOnly` |
| `ActionGroups` | `List<CrudActionGroup<TModel>>` | Declared actions; those with `CrudActionContext.ListPart` appear in the part's toolbar, the item-bound ones also in the row menu |
| `OnBeforeItemAdded`, `OnAfterItemAdded`, `OnBeforeItemRemoved`, `OnAfterItemRemoved` | | As before; deleting several items raises them once per item |
| `OnItemSelected`, `OnItemOpened` | `EventCallback<TModel>` | As on `BaseList`: an item becomes the single selection / the user opens an item |

**Selecting.** The mouse works as in the list: a click on an item selects it (it becomes the anchor), Ctrl/Cmd+click toggles it,
Shift+click selects every item from the anchor to the clicked one (the items are all in memory, so a range is plain positions).
Selected items carry the class `base-listpart-item-selected` (the accent tint of the list, light and dark). More than one
item can only be selected where something consumes the selection (a delete right or a declared item action); without that a
click still selects the single item. The select circles (`select-circle`) are shown while two or more items are selected or
after "Select more"; the header then shows "N selected" and "Deselect all".

**Opening (`Dialog` mode).** The item label is the open link (`base-listpart-item-button`, role `button`): a click on it selects the
item and opens its card, a double click anywhere on the row opens it too, Ctrl/Shift+click on the label only select. The card
is the edit card, or the read-only card ("View") when the part or the card is read-only or the user may read but not modify
the class; a custom `CardType` is only opened read-only when it declares a `ReadOnly` parameter. In `Inline` mode the inputs
stay directly editable: clicking or focusing an input does not change the selection, and the three-dot button and the select
circles (shown in multi-select mode) are the way to the selection, Delete and the item actions.

**The row menu.** The three-dot button (`base-listpart-row-menu`) sits right of the label (at the end of the row in `Inline` mode).
It appears on the hovered row, on the selected row, while the row has the keyboard focus, while its menu is open and always on
screens without hover; the right-click on a row (not in `Inline` mode, where the inputs keep the browser menu) and Shift+F10 /
the context-menu key on a focused control open the same menu. Entries, in the order of the list: **View** or **Edit**, **Delete**
(with the count, "Delete (3)", when the item belongs to a multi-selection; the items are removed through
`OnBeforeItemRemoved`/`OnAfterItemRemoved` one by one, a cancelled one stays), the declared item actions with
`CrudActionContext.ListPart` and a scope other than `None` (one block per action group; an action that is not enabled is greyed out
with its reason as tooltip), a divider, then **Select more**, **Select all** and **Deselect all**. The per-item trash button of
earlier versions is the Delete entry now. A menu opened on an item outside the selection selects that item first.

**The toolbar.** `BaseActionToolbar` receives the selection, so declared actions have the same enabled state and feedback as in the
list (see action scope): a `Single` action needs exactly one selected item, a `Multiple` action at least one, `Enabled(item)` must
hold for every selected item, the disabled button explains why, and while the pointer or focus is on it the blocking items carry
`base-listpart-item-blocked` (warning bar and icon, like `base-list-row-blocked`). `Add` stays in the header.

**Keyboard** (focus on an item label, select circle or the three-dot button; keys typed in an inline editor are never taken):

| Key | Effect |
|---|---|
| Up / Down | Outside multi-select mode: moves the focus and replaces the selection with the new item. In multi-select mode (select circles shown, either after "Select more" or with two or more selected items): moves only the focus, the selection stays |
| Ctrl+Up / Ctrl+Down | Always moves only the focus (the selection stays) |
| Shift+Up / Shift+Down | Extends the selection from the anchor |
| Space | Toggles the focused item in multi-select mode, otherwise selects it |
| Enter | Opens the focused item (`Dialog` mode) |
| Ctrl+A | Selects all items |
| Escape | Closes the open menu, otherwise clears the selection and leaves multi-select mode. It is taken before a card that hosts the part can close on it |
| Shift+F10 / context-menu key | Opens the menu of the focused item |

The part defines the interaction variables it needs on `.base-listpart` itself (`--base-list-control-fill*` with the hover rule for
touch screens, `--base-listpart-row-selected-fill`), so it does not depend on a surrounding `.base-list`. Closing the part's menu or
choosing Select more / Select all / Deselect all puts the keyboard back on the item the menu was opened for.

### `BaseLookupDialog<TModel>`

Modal lookup dialog with search and paging, used by the navigation "Browse…" button or opened manually. It is the content
of a dialog (a `BaseDialogInstance`) and returns the picked item as the dialog result:

```csharp
var result = await DialogService.ShowLookupDialogAsync(title, dataProvider, displayPropertyName: "Name");
var picked = result.Cancelled ? null : (Category?)result.Value;
```

`ShowLookupDialogAsync<TModel>` is an extension of `IDialogService` in `BlazorBase.CRUD.Components`; the dialog needs the
`FluentProviders` of the layout like every other dialog.

### `BaseActionGroup<TModel>` / `BaseAction<TModel>` / `BaseActionToolbar<TModel>`

Declarative child components mirroring the fluent action API. Both approaches can be mixed — declarative items and builder configuration are merged at runtime.

`CrudActionContext` (flags): `ListToolbar`, `ContextMenu`, `Card`, `ListPart`, `All`.

#### Action scope, enabled state and visibility

An action declares which rows it works on with `CrudActionScope` (`Scope`; fluent `.Scope(...)`, `.SingleAction()`,
`.BulkAction()`; component parameter `Scope`):

| Scope | Meaning | Default `Contexts` |
|---|---|---|
| `None` (default) | independent of any row, always enabled | `ListToolbar \| Card \| ListPart` (never the row menu) |
| `Single` | exactly one row | `All` (toolbar, row/right-click menu, card, list part) |
| `Multiple` | one or more rows | `All` |

`IsBulkAction = true` (`.BulkAction()`, parameter `IsBulkAction`) stays supported and is the same as `Scope = Multiple`. An
explicitly set `Contexts` (`ShowIn(...)`, parameter `Contexts`; `HasExplicitContexts`) always wins over the default.

**Enabled state.** In the list toolbar a `Single` action is enabled with exactly one selected row, a `Multiple` action
with at least one. `Enabled(item)` (`Func<TModel, bool>`) is a per-row predicate: a `Multiple` action is enabled only
while it holds for every selected row. In the row/right-click menu the action works on the clicked row (a `Multiple`
action on the whole selection when the clicked row belongs to a multi-selection) and is greyed out when it is not enabled.

**Why is it disabled.** A disabled toolbar button shows the reason as tooltip and exposes it through `aria-describedby`
(resource keys `ActionSelectRowFirst`, `ActionSelectRowsFirst`, `ActionSelectExactlyOne`, `ActionNotPossibleForRow`,
`ActionNotPossibleForRows` = "Not possible for {0} of {1} selected rows"). The action's own text is appended:
`DisabledReason` (static text, resolved through the list's `Localizer`) and/or `DisabledReasonProvider`
(`Func<TModel, string?>`, per blocking row, wins over the static text; fluent `.DisabledReason(string)` /
`.DisabledReason(Func<TModel, string?>)`). The row menu shows the same text as tooltip of the greyed entry.
While the pointer or the keyboard focus is on a disabled button, `BaseActionToolbar` raises `OnBlockedItemsChanged`
with the blocking selected rows and `BaseList` marks them with the CSS class `base-list-row-blocked` (warning-colored
left border and warning icon in the first column; theme token `--color-warning`); leaving the button clears the mark.

**Visibility.** The async `Visible` predicate (actions and groups; `CrudActionVisibilityArgs.Item` and `.SelectedItems`) is now
evaluated. In the toolbar it runs when the selection changes, with `Item` = the single selected row (null when none or
several); a `Multiple` action must pass for every selected row. In the row menu it runs when the menu opens, with `Item` =
the clicked row. `VisibleForRoles` works as before. Use `Visible` for "should this exist", `Enabled` for "can it run now".

**Event arguments.** `CrudActionEventArgs.Item` is the selected/clicked row for `Single` and the clicked row for `Multiple`
in the row menu; `SelectedItems` holds the rows the action works on (the selection for `Multiple`).

Behavior notes: an action without a scope is no longer offered in the row menu unless `Contexts` includes `ContextMenu`; a
row-menu action now refreshes the list afterwards (like the toolbar). `ContextMenuActions` (free render fragment) stays
supported for other sites (legacy) and renders after the declared actions, separated by a divider; new code declares actions.

#### Header layout and the more-actions menu

The list header is one row, in this order from left to right: **Add** (primary), the declared `ListToolbar` actions ordered by
group `Order`, the entry count, the selection info (only in multi-select mode: "N selected" and "Deselect all"), and at the far
right the **Filter** button with its quick filter (a list without filtering has no such group). The actions sit on the left,
where they have room to grow; the filter group is pushed to the right edge, also when the header wraps onto a second row, and
the quick-filter dropdown opens right-aligned with its control so it stays inside the window.
There are no Edit/View/Delete buttons in the header; those live in the row menu (three-dot button and right-click).

All header buttons (Filter, quick filter, every declared action, group menu buttons, the more button) use
the same fill variables `--base-list-control-fill`, `-hover` and `-active` (defined in `BaseList.razor.css`), so rest, hover
and pressed look identical in light and dark; on a screen without hover the hover fill equals the resting one. A disabled
item-bound action is greyed out but still shows its reason on hover and keyboard focus (see "Why is it disabled").

`BaseActionToolbar` has the parameter `AllowOverflow` (default `false`; `BaseList` sets it):

- A small script (`js/baseActionToolbar.js`) measures whether the declared actions fit next to the other header controls.
  If they do not, **all** declared actions collapse into one "More actions" (`MoreActions`, de "Weitere Aktionen") menu
  instead of wrapping the header onto a second row; Filter, quick filter, count and Add always stay visible. Without the
  script the actions simply stay inline.
- The more menu is the toolbar's own markup (position fixed, placed inside the viewport, Esc / arrow keys / overlay click),
  not a `FluentMenu`, because it must keep its hover and focus handling for disabled entries. Entries keep the enabled state, the reason tooltip and the blocked-row marking of the
  toolbar buttons (hovering or focusing a disabled entry marks the blocking rows); item-independent actions stay clickable.
  A multi-action group appears as a heading with its actions below it.
- With `AllowOverflow` a multi-action group is also rendered as this own menu (button with the group caption, icon and a
  chevron) instead of a `FluentMenu` opened from a button. Without it (card and list-part headers) the group is that `FluentMenu` (button with `Id`, menu with `Trigger`, items with `OnClick`).
- At 640 px and below the header never wraps: Add shows only its icon (the text stays as the accessible name `aria-label`),
  the filter group gives way (quick filter shrinks, text ellipsis) and the count stays on one line.

Other sites: nothing to change. Hosts that use `BaseActionToolbar` themselves can set `AllowOverflow="true"` when the toolbar
sits in a row that must not wrap (the parent element must be the row; the toolbar measures its siblings).

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
| `DefaultSortAttribute(property)` | class | The order a list shows until the user sorts; repeatable, `Descending`, `Order` |
| `UtcDateTimeAttribute` | property | Marks a `DateTime` without a kind as a UTC instant, so lists show it in the viewer's local time |
| `MobileVisibleAttribute` | property | The part (`Role`) and `Order` of the property on the tile a list shows on a narrow screen, see "Smartphone tiles" |

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

### Built-in data provider implementations

- **`DbContextBaseDataProvider<T>`** — server-side, backed by EF Core `DbContext`, with dynamic field projection, PATCH-only updates and optimistic concurrency.
- **`HttpBaseDataProvider<T>`** — client-side, backed by REST calls over `HttpClient`; used in WASM/MAUI clients.
- **`InMemoryBaseDataProvider<T>`** — read-only, backed by a host-supplied in-memory list/delegate, for precomputed or statistics-style data with no real backing store.

```csharp
var provider = new InMemoryBaseDataProvider<ProductStatistic>(() => CurrentStatistics);
```

```razor
<BaseList DataProvider="provider" TModel="ProductStatistic">
    ...
</BaseList>
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

`OnBeforePatchAsync` receives CLR-typed values, not raw `JsonElement`s. `DbContextBaseDataProvider<T>.PatchAsync` converts every value whose field name resolves to a writable, patchable property of `TModel` (that is, the same fields `ApplyPatchFields` would later write — not excluded via `[CrudAccess]`, not a navigation property, not an `AuditModel` property) from `JsonElement` to that property's declared type **before** any interceptor runs: a JSON string becomes a `string`, a JSON number/boolean becomes the matching numeric/`bool` type, a nullable enum, `Guid?`, `decimal?` or `DateTime?` field is deserialized into its CLR type, and a JSON `null` becomes a CLR `null`. A field name that does not resolve to such a property (unknown name, read-only, excluded, navigation or audit property) is left as the original `JsonElement` so existing skip behavior is unchanged. An interceptor that assigns a new value to `changedFields` (e.g. `changedFields["Name"] = trimmed`) keeps working — typed or `JsonElement`, the value is applied the same way afterwards. The conversion is centralized in `PatchFieldValueConverter`, which `ApplyPatchFields` also calls for the same `JsonElement` branch, so both call sites share one conversion.


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
| `AddBlazorBaseCrudComponents()` | Interactive UI services the CRUD components depend on (`IConfirmationService`, default `FluentUiConfirmationService`, needs the FluentUI `IDialogService`) — Wasm ✓ / Maui ✓ / interactive, Server —; call after `AddBlazorBaseFluentUI()` |
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

`BlazorBase.Components.Diff.DiffViewer` renders a `FileDiff` in three modes: Inline, Side-by-side, or New-only. Uses the vendored highlight.js 11.9.0 (common-languages build, BSD-3, no CDN) for optional syntax highlighting.

### Diff model namespace

The five diff types live in `BlazorBase.Components.Models.Diff` (moved from `DevPortal.Shared.Modules.Repositories.Diff`; JSON wire format unchanged):

```csharp
namespace BlazorBase.Components.Models.Diff;

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
namespace BlazorBase.Components.Diff;

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

The `github-dark.min.css` theme is injected once into `<head>` by `syntaxHighlight.js` — no consumer-side `<link>` tag required. The JS module path is `./_content/BlazorBase.Components/js/syntaxHighlight.js`.

### Usage

```razor
<DiffViewer Diff="@myFileDiff" Language="csharp" />
<DiffViewer Diff="@myFileDiff" @bind-Mode="@CurrentMode" EnableSyntaxHighlight="false" />
```

---

## FileTree Component

`BlazorBase.Components.Files.FileTree` renders an ARIA-compliant navigable file tree. Build the node hierarchy from a flat path list with `FileTreeBuilder.Build(paths)`.

### Models (`BlazorBase.Components.Models.FileTree`)

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
            .Scope(CrudActionScope.Multiple)
            .Enabled(item => !item.IsActive)
            .DisabledReason("Already active")
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
    <FluentText As="TextTag.H1" Size="TextSize.Size600" Weight="TextWeight.Semibold">@Localizer["Title"]</FluentText>

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
    <FluentText As="TextTag.H1" Size="TextSize.Size600" Weight="TextWeight.Semibold">@Localizer["Title"]</FluentText>

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
    <FluentText As="TextTag.H1" Size="TextSize.Size600" Weight="TextWeight.Semibold">@Localizer["Title"]</FluentText>

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

        <FluentText As="TextTag.H2" Size="TextSize.Size500" Weight="TextWeight.Semibold">
            @string.Format(Localizer["SubtypesOf"], SelectedType.Name)
        </FluentText>

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
    <FluentText As="TextTag.H1" Size="TextSize.Size600" Weight="TextWeight.Semibold">@Localizer["Title"]</FluentText>

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
                        <FluentBadge Appearance="BadgeAppearance.Filled" Color="BadgeColor.Brand">@Localizer["Running"]</FluentBadge>
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
    <FluentText As="TextTag.H1" Size="TextSize.Size600" Weight="TextWeight.Semibold">@Localizer["Title"]</FluentText>

    @if (Model is null)
    {
        <FluentSpinner />
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
        <FluentSpinner />
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
    <FluentText As="TextTag.H1" Size="TextSize.Size600" Weight="TextWeight.Semibold">@Localizer["Title"]</FluentText>

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
                .SingleAction()
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

    <FluentText>@Summary</FluentText>
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

`BlazorBase.Components.Editors.RichTextEditor` is a generic, vendored WYSIWYG rich-text editor that edits and produces an **HTML string**. It has no external dependencies and loads no CDN resources — the JS module ships as a static web asset of `BlazorBase.Components`.

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

The component uses CSS isolation (`RichTextEditor.razor.css`). Colors and radii come from the BlazorBase semantic CSS custom properties (`--color-bg-surface`, `--color-text-primary`, `--color-accent`, `--radius-sm`, …) so the editor automatically follows the host app's theme.

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

`BlazorBase.Components.Html.SanitizedHtml` renders an HTML string safely inside a `<div class="sanitized-html">` root element.

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

`BlazorBase.Components.Sanitization.IHtmlSanitizer` is a generic abstraction for HTML sanitization. It is **interface-only** — no implementation is provided here.

```csharp
namespace BlazorBase.Components.Sanitization;

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
