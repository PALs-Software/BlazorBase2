# BlazorBase.Dashboard

A Razor Class Library for **role center** start pages in the style of Business Central: a headline, cues (tiles with a
traffic-light style and a drill-down), chart parts and list parts, each shown only to the users it is meant for, loaded
and refreshed independently.

> Target framework: `net10.0` · the components and the client stores are browser-safe (the server-side storage lives in
> `BlazorBase.Dashboard.Server` and is never referenced by client code) · works in Server, WebAssembly and MAUI ·
> depends on FluentUI and [BlazorBase.Chart](BlazorBase.Chart.md).

---

## Table of Contents

- [Concepts](#concepts)
- [Getting started](#getting-started)
- [Registering parts](#registering-parts)
- [Writing a part](#writing-a-part)
- [RoleCenter](#rolecenter)
- [Cues](#cues)
- [Cue thresholds](#cue-thresholds)
- [Headline and insight providers](#headline-and-insight-providers)
- [List parts and chart parts](#list-parts-and-chart-parts)
- [Drill-down links and filter presets](#drill-down-links-and-filter-presets)
- [Refresh](#refresh)
- [Visibility](#visibility)
- [Personalization](#personalization)
- [Layout, phones and theming](#layout-phones-and-theming)
- [Accessibility](#accessibility)
- [Localization](#localization)
- [Public API at a glance](#public-api-at-a-glance)

---

## Concepts

| Concept | Type | What it is |
|---|---|---|
| Part | any Blazor component | A card of the start page. Needs no required parameters; it injects what it needs. May implement `IDashboardRefreshable`. |
| Registration | `DashboardPartRegistration` | `Id`, `Priority`, `Columns` (1-12), `ComponentType`, visibility rule, optional `Title`. |
| Registry | `IDashboardPartRegistry` | The catalogue modules add their parts to; the role center reads it. |
| Role center | `RoleCenter` | The responsive 12-column grid that renders the visible parts in priority order. |
| Part frame | `DashboardPartFrame` (internal use) | Around every part: loading skeleton, error isolation with retry card, collapse when empty. |
| Cue | `Cue`, `CueGroup`, `CueStyle` | A tile with one number, a label, a traffic-light style and a drill-down link. |
| Headline | `HeadlinePart`, `IHeadlineInsightProvider` | A banner with a time-of-day greeting, rotating insights and quick actions. |
| Building blocks | `DashboardCard`, `ListPart<T>`, `ChartPart` | The frame of a part, a top-N list, a chart card. |

A user can hold several roles, so there is **one page composed of parts, each with a visibility rule**, not one profile
per user. Modules register their own parts; the page is just `<RoleCenter />`.

---

## Getting started

1. Reference the project (`BlazorBase.Dashboard.csproj`) from the client (and from the server project when it renders
   the page).
2. Load Chart.js once in the host page if you use `ChartPart` (see [BlazorBase.Chart](BlazorBase.Chart.md)):
   `<script src="_content/BlazorBase.Chart/js/chart.umd.min.js"></script>`.
3. Register the dashboard:

```csharp
services.AddBlazorBaseDashboard(options =>
{
    options.AutoRefreshInterval = TimeSpan.FromMinutes(5);             // default; Zero switches it off

    options.AddPart<MyUsagePart>("my-usage", priority: 10, columns: 7);
    options.AddPart<TodoPart>("todo", priority: 20, columns: 5);
    options.AddPart<GovernancePart>("governance", 30, 12, DashboardVisibility.ForRoles("Admin", "AiTelemetryUser"), title: "AI governance");

    options.AddHeadlineInsightProvider<BoostInsightProvider>();
});
```

`AddBlazorBaseDashboard` can be called by several modules; parts and providers add up, an id can only be registered once.
It also registers `TimeProvider.System` (when none is registered), `DashboardInterop`, `DashboardClock` and
`DashboardSettings`. FluentUI (`AddBlazorBaseFluentUI` from BlazorBase.Components, or `AddFluentUIComponents` where BlazorBase.Components is not used), localization (`AddLocalization`) and the authorization services
(`AuthenticationStateProvider`) are expected from the host as for every other BlazorBase component.

4. Place the role center on the start page:

```razor
@using BlazorBase.Dashboard

<RoleCenter>
    <Headline>
        <HeadlinePart>
            <Actions>
                <a href="/budget-boosts/new">Request budget boost</a>
                <a href="/my-ai-statistics">My AI statistics</a>
            </Actions>
        </HeadlinePart>
    </Headline>
    <MetaContent><span>@Month</span></MetaContent>
</RoleCenter>
```

The scoped styles of the package are part of the host's `*.styles.css` bundle automatically.

---

## Registering parts

```csharp
options.AddPart<TPart>(string id, int priority = 0, int columns = 12,
                       Func<DashboardPartContext, ValueTask<bool>>? isVisibleAsync = null,
                       string? title = null);
options.AddPart(DashboardPartRegistration registration);
```

| Property | Meaning |
|---|---|
| `Id` | Stable, unique. Key of the rendered part, target of `RefreshAsync(id)`. |
| `Priority` | Sort key, **lower comes first**; ties keep the registration order. |
| `Columns` | Width on the 12-column desktop grid, clamped to 1-12. Narrower containers render the part full width. |
| `ComponentType` | A component type; the registry rejects anything else. |
| `IsVisibleAsync` | Visibility rule, default `DashboardVisibility.ForAnyUser()`. |
| `Title` | Optional name shown in the loading skeleton, the retry card and the personalization edit mode (without it the part id is humanized). |
| `TitleFactory` | Optional `Func<IServiceProvider, string?>` for a localized name; wins over `Title` and runs on every render, so it follows the current culture. Example: `titleFactory: sp => sp.GetRequiredService<IStringLocalizer<GovernancePart>>()["Title"].Value`. |

Registrations can also be created with `DashboardPartRegistration.For<TPart>(...)` and added to any
`IDashboardPartRegistry` directly (for example from a module initializer).

---

## Writing a part

A part is an ordinary component. It does **not** receive parameters; it injects what it needs:

```razor
@inherits DashboardPartBase
@inject IMyUsageService Usage

<DashboardCard Title="My AI usage" MoreHref="/my-ai-statistics" MoreText="My AI statistics" Loading="@(Model is null)">
    @if (Model is not null)
    {
        <CueGroup Label="My AI usage">
            <Cue Wide Value="@($"{Model.UsedPercent:0} %")" Label="Budget used" RingPercent="Model.UsedPercent"
                 Style="CueStyleResolver.Resolve(Model.UsedPercent, Thresholds)" Href="/my-ai-statistics" />
            <Cue Value="@Model.Credits.ToString("N0")" Label="AI credits this month" Href="/my-ai-statistics" />
        </CueGroup>
    }
</DashboardCard>

@code {
    private static readonly CueThresholds Thresholds = CueThresholds.HigherIsWorse(ambiguousFrom: 75, unfavorableFrom: 90);

    private UsageModel? Model { get; set; }

    protected override async Task LoadAsync() => Model = await Usage.GetAsync(DisposalToken);
}
```

`DashboardPartBase` is optional. It

- reports *loading* to the frame while `LoadAsync` runs for the first time (the frame shows a skeleton over the part),
- implements `IDashboardRefreshable` by running `LoadAsync` again and re-rendering (the old data stays visible; no
  skeleton for a refresh),
- lets an exception of `LoadAsync` reach the part's retry card,
- exposes `DisposalToken` (cancelled when the part leaves the page) and `ReportEmpty(bool)`.

Without the base class, implement `IDashboardRefreshable` (`Task RefreshAsync()`) yourself and take the cascading
`IDashboardPartHost` when you want to talk to the frame:

| `IDashboardPartHost` | Effect |
|---|---|
| `ReportLoading()` / `ReportLoaded()` | Show / remove the skeleton over the part. |
| `ReportEmpty(bool isEmpty = true)` | Collapse the part completely (no frame, no gap in the grid) while it has nothing to show; call with `false` when it does again. A part that failed is never collapsed. |

An exception thrown while the part renders, initializes or runs a lifecycle method is caught by an error boundary around
**that part only**; the rest of the page keeps working. The part is replaced by a small card ("This part could not be
loaded." + *Retry*); *Retry* creates a fresh instance of the part. A failure during a refresh shows the same card.

---

## RoleCenter

```razor
<RoleCenter Headline="..." MetaContent="..." Tools="..." AutoRefreshInterval="TimeSpan.FromMinutes(2)" Class="..." />
```

| Parameter | Description |
|---|---|
| `Headline` | Content above the meta row, normally a `HeadlinePart`. The headline joins the refresh. |
| `MetaContent` | Start of the meta row, before "Updated hh:mm", for example the month the numbers belong to. |
| `Tools` | End of the meta row, before the refresh button (and before the *Personalize* button when a layout store is registered). |
| `AutoRefreshInterval` | Overrides the interval configured in `AddBlazorBaseDashboard` for this role center; `TimeSpan.Zero` = off. |
| `LayoutKey` | The key the user's personal layout is stored under; defaults to `"/"` (the start page). Set it when a host has more than one role center. See [Personalization](#personalization). |
| `Class` | Extra CSS class on the root. |
| `RefreshService` (property) | The `IDashboardRefreshService` the role center cascades; reachable through `@ref`. |

The meta row shows **"Updated hh:mm"** (the time of the last refresh, in the user's time zone and culture) and a
**Refresh** button. Parts render in priority order inside a 12-column grid; hidden parts and collapsed parts take no space.

---

## Cues

```razor
<CueGroup Label="To do">
    <Cue Value="3" Label="Boosts to decide" Style="CueStyle.Unfavorable" Href="/pending-boosts" />
    <Cue Value="1" Label="My open requests" Style="CueStyle.Ambiguous" Href="@ListPresetLink.Build("/budget-boosts", "my-pending")" />
    <Cue Wide Value="$1,214" Label="Cost month to date" SubValue="Projection $1,840 · budget $2,000"
         Style="CueStyle.Ambiguous" SparklinePoints="@Monthly" Href="/telemetry-overview" />
    <Cue Wide Value="72 %" Label="Budget used" SubValue="$36.00 of $50.00" RingPercent="72" Href="/my-ai-statistics" />
    <Cue Value="48" Unit="/62" Label="Active users" />
</CueGroup>
```

| `Cue` parameter | Description |
|---|---|
| `Value` | The already formatted number or text. |
| `Unit` | Small text right behind the value (`/62`, `min`). |
| `SubValue` | A line at the bottom (best with `Wide`). |
| `Label` (required) | What is counted; part of the accessible name. |
| `Style` | `CueStyle`: `None`, `Favorable`, `Ambiguous`, `Unfavorable`, `Subordinate`. |
| `Href` | With a value the tile is a real `<a>`; without it a non-link tile (`role="group"`). |
| `Wide` | 252 px instead of 120 px; spans both columns on phones. |
| `SparklinePoints` | `IReadOnlyList<double>`; two or more points draw a tiny inline SVG trend. |
| `RingPercent` | `double?` 0-100 (clamped); draws an inline SVG progress ring in the style colour. |
| `Loading` | Skeleton instead of the content; `aria-busy`. |
| `Class`, other attributes | Applied to the root element. |

Appearance: the tile is a fixed 96 px high (120 × 96 px, wide 252 × 96 px) so rows align. A 4 px stripe on the leading
edge carries the style colour, a small icon in the corner repeats it as a shape (check, triangle, exclamation mark), and
the number itself is coloured **only for `Unfavorable`**. The label wraps up to three lines, a long value is cut with an
ellipsis, so use `Wide` for long values. Colours come from the theme tokens (`--color-success`, `--color-warning`,
`--color-danger`, `--color-text-muted`), so light and dark mode both keep their contrast.

`CueGroup` lays the tiles out as a wrapping row of 10 px gaps; in a role center narrower than 640 px it becomes two equal
columns.

---

## Cue thresholds

```csharp
var thresholds = CueThresholds.HigherIsWorse(ambiguousFrom: 75, unfavorableFrom: 90);
CueStyle style = CueStyleResolver.Resolve(usedPercent, thresholds);
```

| Direction | Factory | Rule (both boundaries inclusive) |
|---|---|---|
| `HigherIsWorse` | `CueThresholds.HigherIsWorse(ambiguousFrom, unfavorableFrom)` | `value >= unfavorableFrom` → Unfavorable; `value >= ambiguousFrom` → Ambiguous; else Favorable. Needs `unfavorableFrom >= ambiguousFrom`. |
| `HigherIsBetter` | `CueThresholds.HigherIsBetter(ambiguousFrom, unfavorableFrom)` | `value <= unfavorableFrom` → Unfavorable; `value <= ambiguousFrom` → Ambiguous; else Favorable. Needs `unfavorableFrom <= ambiguousFrom`. |

`CueStyleResolver.ForCount(count, whenPositive, whenZero = CueStyle.None)` covers the common "only matters above zero"
cues: `ForCount(boosts, CueStyle.Unfavorable)`.

---

## Headline and insight providers

`HeadlinePart` is a neutral card (`--color-bg-elevated` surface, 1 px `--color-border`, a 4 px `--color-accent` top line,
`--color-text-primary` text) with

- a **greeting by time of day** and culture ("Good morning, Patrick" / "Guten Morgen, Patrick"): 05:00 to 11:59 morning,
  12:00 to 17:59 afternoon, otherwise evening. The time is the user's: the first component asks the browser for its UTC
  offset once (`DashboardClock`), so a server-rendered app does not greet with the server's time zone;
- the **name**: the `UserName` parameter, otherwise the given name claim (`ClaimTypes.GivenName` / `given_name`), then
  the display name claim (`name` / identity name); a login that is an e-mail address is never shown;
- up to `MaxInsights` (default 3) **insights**, ordered by priority across all providers (lower first), rotating every
  `RotationInterval` (default 8 s);
- an `Actions` slot for quick actions: plain `<a>` / `<button>` elements, styled as buttons; on phones a horizontally
  scrollable row below the text. The **first** action is the primary button (filled, bold); the others are outline
  buttons. A single action is the primary one. The primary fill comes from `--hl-primary-fill` (hover
  `--hl-primary-fill-hover`, text `--hl-primary-text`), defined on `.hl` from `--color-accent`, `--color-accent-hover` and
  `--color-text-on-accent`, which switch with the theme. A theme can override the three variables.

```csharp
public sealed class BoostInsightProvider(IBoostService boosts) : IHeadlineInsightProvider
{
    public async ValueTask<IReadOnlyList<HeadlineInsight>> GetInsightsAsync(CancellationToken cancellationToken)
    {
        var waiting = await boosts.CountWaitingAsync(cancellationToken);
        return waiting == 0 ? [] : [new HeadlineInsight(10, $"**{waiting} budget boosts** are waiting for your decision.")];
    }
}
```

`HeadlineInsight(int priority, string text)` supports **bold segments** with `**double asterisks**`; the text is never
interpreted as HTML. `HeadlineInsight(int priority, RenderFragment content)` renders markup (for a link). A provider
that throws is skipped; the others still show. Providers are scoped (`options.AddHeadlineInsightProvider<T>()` or
`services.AddHeadlineInsightProvider<T>()`).

Rotation rules: the dots are real `<button>`s (`aria-label` "Show insight 2 of 3", `aria-current` on the shown one, 32 px
hit area); rotation **pauses while the pointer or the keyboard focus is inside the banner**, **stops for good** once the
user picked an insight, and is **disabled under `prefers-reduced-motion`** (the dots stay usable). A single insight
shows no dots. The timer is created through `TimeProvider`, so tests can drive it.

---

## List parts and chart parts

```razor
<ListPart TItem="BoostRow" Title="Boosts waiting for my decision" Items="Rows" TotalCount="Total"
          ShowAllHref="@ListPresetLink.Build("/budget-boosts", "pending")" CollapseWhenEmpty="true">
    <ItemTemplate Context="row">@row.User · +@row.Amount</ItemTemplate>
</ListPart>
```

| `ListPart<TItem>` parameter | Description |
|---|---|
| `Title`, `Tools`, `Class` | Header of the card. |
| `Items` | The rows; at most `MaxItems` (default 5) are rendered, not virtualized. Pass the already limited server result. |
| `ItemTemplate` (required) | `RenderFragment<TItem>`, placed in a `<li>` of a `<ul>`. |
| `EmptyTemplate` | Instead of the default "Nothing to show.". |
| `ShowAllHref`, `ShowAllText`, `TotalCount` | The "Show all" link; the default text is "Show all" or "Show all (N)" with `TotalCount`. |
| `Loading` | Skeleton rows. |
| `CollapseWhenEmpty` | Reports empty to the frame (collapses the part) instead of showing the empty state. |

`DashboardCard` is the frame both use and can be used alone: `Title` (an `h2` that labels the section), `MoreHref` /
`MoreText`, `Tools`, `ChildContent`, `Loading`, `Class`.

```razor
<ChartPart Title="Cost trend (6 months)" MoreHref="/telemetry-trends" MoreText="Telemetry trends" Height="220px"
           Summary="Monthly cost, rising from 420 to 920 dollars">
    <ChartContent Context="palette">
        <BarChart Style="@ChartPart.FillStyle" Labels="@Months"
                  Datasets="@(new List<ChartDataset> { new() { Data = Costs, BackgroundColor = [palette.Accent] } })" />
    </ChartContent>
</ChartPart>
```

`ChartPart` puts a card around a `BlazorBase.Chart` chart with a fixed responsive height (`Height`, a CSS length, capped
at 200 px on phones). A canvas cannot use CSS variables, so the chart fragment receives a `ChartPalette` with the
**resolved colours of the current theme** (`Text`, `TextSecondary`, `Grid`, `Accent`, `AccentMuted`, `Success`,
`Warning`, `Danger`, `Neutral`); the dashboard script also sets `Chart.defaults.color` / `borderColor` from the tokens, and
the part renders again when the light/dark theme changes. `Summary` is the text alternative (the area is `role="img"`);
without it the title is used. `ChartContent` and a plain `ChildContent` can be used together.

---

## Drill-down links and filter presets

A cue is only trustworthy when the number equals the row count of the list it opens. Give the target list a **filter
preset** and link to it with the `?preset=<id>` deep link of `BaseList` (see
[BlazorBase.CRUD.md](BlazorBase.CRUD.md#saved-filters-presets-and-defaults), "Deep link a preset"):

```csharp
Href = ListPresetLink.Build("/budget-boosts", "my-pending");   // "/budget-boosts?preset=my-pending"
```

The preset applies on the first load of the list, wins over the saved default for that visit only and is shown as the
active choice of the quick filter. Count the cue with the same predicate on the server; never count or load rows on the
client (production lists hold 100,000+ rows).

---

## Refresh

| Trigger | Behaviour |
|---|---|
| Open | Every part loads itself. |
| *Refresh* button | `RefreshAllAsync()`; the button is disabled and `aria-busy` while it runs. |
| Auto refresh | Every `AutoRefreshInterval` (default 5 minutes), **silently**, and only while the tab is visible. A tick while the tab is hidden is remembered; when the tab is shown again it refreshes at once if data is older than the interval or a tick was missed. Stops on dispose. |
| A part | The cascaded `IDashboardRefreshService`. |

```razor
@code {
    [CascadingParameter] public IDashboardRefreshService? Refresh { get; set; }

    private async Task AcceptAsync(Boost boost)
    {
        await Boosts.AcceptAsync(boost);
        await Refresh!.RefreshAsync("todo", "headline");   // or: await Refresh!.RefreshAllAsync();
    }
}
```

| `IDashboardRefreshService` | Description |
|---|---|
| `RefreshAllAsync()` | Re-evaluates which parts are visible, then refreshes every `IDashboardRefreshable`. A call during a running refresh joins it. |
| `RefreshAsync(params string[] partIds)` | Refreshes only the named parts (the headline has the id `headline`); unknown ids are ignored. |
| `Register(id, refreshable)` | Includes a component that is not a registered part in the refresh; dispose the result to leave. |

A refresh is fail-soft: a part that throws shows its retry card, the other parts still refresh. "Updated hh:mm" shows
the data age. The only JavaScript involved is `dashboard.js` (tab visibility, reduced motion, UTC offset, theme colours),
imported lazily.

---

## Visibility

```csharp
DashboardVisibility.ForAnyUser()                         // the default
DashboardVisibility.ForRoles("Admin", "AiTelemetryUser") // any of the roles (AuthenticationState user, IsInRole)
DashboardVisibility.ForUsers(user => user.HasClaim("team-lead", "true"))
options.AddPart<TPart>("id", isVisibleAsync: async context =>        // async: policy, server call
    (await authorization.AuthorizeAsync(context.User, "CanApprove")).Succeeded);
```

`DashboardPartContext` carries `User` (`ClaimsPrincipal`), `Services` (the scoped provider) and a `CancellationToken`.
Rules run when the role center initializes and again on every `RefreshAllAsync`, so a part appears or disappears when the
user's rights change. A rule that throws **hides** the part (fail closed). Visibility on the client is presentation only:
the data of a part must still be protected on the server.

---

## Personalization

Every user can hide parts and change their order. The arrangement is a `DashboardLayout` (`Parts`: one
`DashboardPartPreference` with `PartId`, `Hidden` and `Order` per part), stored **per user and per dashboard key**
(`RoleCenter.LayoutKey`, default `"/"`) in an `IDashboardLayoutStore`. It works like the saved list filters of
[BlazorBase.CRUD](BlazorBase.CRUD.md): register a store and the feature appears; **no store registered = no
personalization UI**, the role center behaves exactly as before.

| Host | Registration | Storage |
|---|---|---|
| WASM / MAUI with a server | `services.AddBlazorBaseDashboardLayoutsClient(c => c.BaseAddress = new Uri(api, "api/dashboard-layouts/"), b => b.AddHttpMessageHandler<…>())` | Server, per user (`HttpDashboardLayoutStore`) |
| Server | `services.AddBlazorBaseDashboardLayouts<TContext>()` + `app.MapBlazorBaseDashboardLayouts()` + `modelBuilder.ApplySavedDashboardLayoutConfiguration<TUser>()` in `OnModelCreating` (then add an EF migration) | EF Core table `SavedDashboardLayouts` (`EfDashboardLayoutRepository<TContext>`) |
| No server storage | `services.AddBlazorBaseDashboardLayoutsLocalStorage()` or `AddBlazorBaseDashboardLayoutsInMemory()` | Browser `localStorage` (per browser profile, not per user) / process memory |

`TContext` must be registered with `AddDbContextFactory<TContext>`. The server-only types live in the namespace
`BlazorBase.Dashboard.Server` (`SavedDashboardLayout`, `IDashboardLayoutRepository`, `EfDashboardLayoutRepository`,
`MapBlazorBaseDashboardLayouts`, `AddBlazorBaseDashboardLayouts`, `ApplySavedDashboardLayoutConfiguration`) and are never
referenced by client code; the client only uses `IDashboardLayoutStore`, `HttpDashboardLayoutStore` and the DTOs.
`ApplySavedDashboardLayoutConfiguration()` adds no foreign key; the generic overload `<TUser>` adds a cascading one to
the host's user table (hosts without a user table call `IDashboardLayoutRepository.DeleteAllForUserAsync` when a user is removed).

**Endpoints** (`MapBlazorBaseDashboardLayouts(route = "api/dashboard-layouts")`), all `RequireAuthorization()`; the user is
always the authenticated principal, never a request value, so a caller can only read, replace or delete their own layout:

| Verb | Route | Result |
|---|---|---|
| `GET` | `?dashboardKey=/` | `200` with the layout (empty `parts` when never arranged) |
| `PUT` | `?dashboardKey=/` with `{"parts":[{"partId":"...","hidden":false,"order":0}]}` | `204`; `400` when the key or layout is invalid |
| `DELETE` | `?dashboardKey=/` | `204` (also when there was nothing to delete) |

Limits (`DashboardLayoutValidator`): key 1-256 characters, at most **64 parts**, part ids 1-100 characters and unique,
`order` 0-10000. The table has a unique index on user + key.

`IDashboardLayoutStore` has `GetAsync(key)` (a never-arranged dashboard returns an empty layout, not null),
`SaveAsync(key, layout)` and `ResetAsync(key)`.

**How a layout is applied** (`DashboardLayoutArranger`):

1. Role rules always run first. A layout can only reorder and hide the parts the user may see anyway; ids of parts the
   user may not (or no longer may) see, or that no longer exist, are ignored. A layout never grants visibility.
2. The saved layout is read **before the first part is rendered**: a hidden part is neither rendered nor fetched.
3. The saved order replaces `Priority` for this user. A part the layout does not know (added after the user saved) takes
   its default place: right after the part that precedes it in the default order, or at the start when none does.
4. If the store cannot be reached (error, or no answer within `DashboardSettings.LayoutLoadTimeout`, default 5 s) the
   default layout is shown and the page keeps working; the failure is logged as a warning.

**Edit mode.** With a store registered, the meta row shows a *Personalize* button. It switches the role center into edit
mode: an edit bar (*Reset to default*, *Cancel*, *Done*) appears and every part the user may see gets a card header with
**Move up**, **Move down** and **Hide / Show**; hidden parts stay in the list as dimmed cards with *Show*. The live parts
are shown inert underneath, so nothing navigates away by accident. *Done* saves (the order is stored as `0..n-1`, all
role-visible parts are written); *Cancel* discards; *Reset to default* asks for confirmation inline and deletes the
stored layout. A failed save or reset keeps the edit mode open with an alert and the changes intact.

Accessibility of the edit mode: all controls are real `<button>`s with accessible names that contain the part title
("Move Cost trend up", "Hide Cost trend"), at least 44 × 44 px, keyboard operable; a move keeps the focus on the control
that was used (and the end buttons use `aria-disabled` instead of `disabled`, so the focus is not lost); every action is
announced in a polite live region ("Cost trend moved to position 2 of 5."); entering the edit mode moves the focus to
the edit bar, leaving it returns the focus to *Personalize*. On phones the cards are full width with the same controls.

```csharp
services.AddBlazorBaseDashboardLayoutsClient(
    client => client.BaseAddress = new Uri(apiBaseAddress, "api/dashboard-layouts/"),
    builder => builder.AddHttpMessageHandler<CsrfHeaderHandler>());
```

For a host that wants no server storage: `services.AddBlazorBaseDashboardLayoutsLocalStorage();`.

---

## Layout, phones and theming

- **Container queries**, not only the viewport: a container of **1100 px and wider** uses the 12-column grid with the
  registered column widths (16 px gaps); a narrower container renders every part full width; below **640 px** the gaps
  shrink, cue groups become two columns, wide cues span both, chart areas are capped at 200 px and the headline actions
  become a scrollable button row. Each rule is also written as a viewport media query, so the building blocks behave on a
  phone even outside a `RoleCenter`.
- The root of `RoleCenter` is a `container-name: rolecenter` / `container-type: inline-size` container.
- All colours, radii, spacing and fonts come from the BlazorBase/acadon tokens (`--color-*`, `--radius-*`, `--space-*`,
  `--font-*`); there are no hard-coded colours. Link text in cards uses a mix of `--color-accent` and
  `--color-text-primary`, because the plain acadon accent does not reach 4.5:1 on a light surface.
- Touch targets are at least 32 px; the quick-action chips grow to 44 px on touch devices.
- `prefers-reduced-motion` switches off skeleton pulses, the insight fade and the dot animation.

---

## Accessibility

- A cue with `Href` is a real link with `aria-label="<value> <label>, <style in words>"` ("3 Boosts to decide,
  unfavorable"; the sub value is appended). A non-link cue is `role="group"` with the same name. Style words are
  localized: favorable, needs attention, unfavorable, secondary.
- Status is never colour-only: stripe, corner icon (different shapes) and the status word in the accessible name; the
  number is red only for unfavorable.
- Focus is always visible (`--color-border-focus` ring); contrast of text and status colours is at least 5.4:1 in the
  light and the dark theme.
- The role center is a `region` labelled "Dashboard"; every card is a `section` labelled by its `h2`; loading parts are
  `aria-busy`; the retry card is `role="alert"`; "Updated" is a polite `status`.
- Insight rotation pauses on hover and focus, stops after a manual choice and is off under reduced motion.

---

## Localization

Every string the package produces itself comes from `DashboardResources` (`.resx` + `.de.resx` in
`BlazorBase.Dashboard/Localization`), resolved through `IStringLocalizer<DashboardResources>`: cue status words, greetings,
"Updated {0}", *Refresh*, the retry card, "Show all", "Nothing to show.", the insight dot names, and the whole personalization
edit mode (buttons, accessible names, announcements, error texts). Your own texts (titles,
labels) are yours to localize.

---

## Public API at a glance

Namespace `BlazorBase.Dashboard` (and `BlazorBase.Dashboard.Localization` for `DashboardResources`).

| Type | Kind | Purpose |
|---|---|---|
| `DashboardServiceCollectionExtensions` | static | `AddBlazorBaseDashboard(configure?)`, `AddHeadlineInsightProvider<T>()` |
| `DashboardOptions` | class | `AutoRefreshInterval`, `AddPart(...)`, `AddPart<T>(...)`, `AddHeadlineInsightProvider<T>()` |
| `DashboardSettings` | class | Runtime `AutoRefreshInterval`, `LayoutLoadTimeout` |
| `DashboardPartRegistration` | class | `Id`, `ComponentType`, `Priority`, `Columns`, `Title`, `TitleFactory`, `ResolveTitle(...)`, `IsVisibleAsync`, `For<T>(...)` |
| `DashboardPartContext` | record | `User`, `Services`, `CancellationToken` |
| `DashboardVisibility` | static | `ForAnyUser()`, `ForRoles(...)`, `ForUsers(...)` |
| `IDashboardPartRegistry` / `DashboardPartRegistry` | interface / class | `Parts`, `Register(...)` |
| `IDashboardRefreshable` | interface | `RefreshAsync()` |
| `IDashboardPartHost` | interface | `PartId`, `ReportLoading()`, `ReportLoaded()`, `ReportEmpty(bool)` |
| `IDashboardRefreshService` / `DashboardRefreshService` | interface / class | `RefreshAllAsync()`, `RefreshAsync(ids)`, `Register(id, refreshable)` |
| `DashboardPartBase` | abstract component | `LoadAsync()`, `Host`, `DisposalToken`, `ReportEmpty(bool)` |
| `RoleCenter` | component | The grid |
| `DashboardPartFrame`, `DashboardPartErrorBoundary` | components | Internal to `RoleCenter` |
| `DashboardCard` | component | Card frame |
| `Cue`, `CueGroup` | components | Tiles |
| `CueStyle`, `CueDirection`, `CueThresholds`, `CueStyleResolver` | enum / enum / record / static | Traffic-light styles |
| `HeadlinePart`, `IHeadlineInsightProvider`, `HeadlineInsight`, `HeadlineText` | component / interface / class / static | Banner and insights |
| `ListPart<TItem>` | component | Top-N list |
| `ChartPart`, `ChartPalette` | component / class | Chart card and theme colours |
| `DashboardInterop`, `DashboardClock`, `DashboardJsCallback` | classes | JS bridge and user-local time (infrastructure) |
| `DashboardLayout`, `DashboardPartPreference` | classes | The personal arrangement: `Parts` with `PartId`, `Hidden`, `Order`; `DashboardLayout.DefaultKey` |
| `IDashboardLayoutStore` | interface | `GetAsync`, `SaveAsync`, `ResetAsync` per dashboard key |
| `HttpDashboardLayoutStore`, `LocalStorageDashboardLayoutStore`, `InMemoryDashboardLayoutStore` | classes | The store implementations |
| `DashboardLayoutClientExtensions` | static | `AddBlazorBaseDashboardLayoutsClient`, `...LocalStorage`, `...InMemory` |
| `DashboardLayoutArranger`, `DashboardArrangedPart` | static / record | Applies a layout to the role-visible parts |
| `DashboardLayoutValidator`, `DashboardLayoutValidationException`, `DashboardLayoutSerializer` | static / exception / static | Limits, validation and JSON of a layout |
| `BlazorBase.Dashboard.Server`: `SavedDashboardLayout`, `SavedDashboardLayoutEntityConfiguration`, `IDashboardLayoutRepository`, `EfDashboardLayoutRepository<TContext>`, `DashboardLayoutEndpointMapper`, `DashboardLayoutServerExtensions` | server-only | EF table, repository, `MapBlazorBaseDashboardLayouts`, `AddBlazorBaseDashboardLayouts<TContext>` |
