# BlazorBase.Components

Razor Class Library (net10.0). The shared UI layer: application shell, navigation, theming, and the
standalone editors. It references neither `BlazorBase.CRUD` nor `BlazorBase.User` — both of those sit
above it — so an application that only needs a data grid does not have to pull in the auth stack to get
a layout, and a rich-text editor is not a CRUD concept.

---

## Themes

Two default themes ship as one stylesheet of CSS custom properties. Link it once, together with the Fluent UI stylesheet
and tell Fluent not to apply its own page styles (the shell owns the page layout):

```html
<link rel="stylesheet" href="_content/Microsoft.FluentUI.AspNetCore.Components/Microsoft.FluentUI.AspNetCore.Components.bundle.scp.css" />
<link rel="stylesheet" href="_content/BlazorBase.Components/css/blazorbase.css" />
...
<body no-fuib-style>
```

`blazorbase.css` imports `blazorbase-fluent-bridge.css` itself, so one link covers the tokens and the bridge.

Every BlazorBase component stylesheet that paints a colour, a size or a font reads the tokens below — the shell, both
navigation components, the whole CRUD layer, the editors, the file components. Application code uses the same
names, and nothing else: never a Fluent token (`--colorNeutral…`, `--fontSize…`) and never a selector on a Fluent
element or its parts. The Fluent components themselves are painted from the same tokens.

### The Fluent bridge

Fluent UI v5 paints from Fluent 2 tokens (`--colorBrandBackground`, `--colorNeutralForeground1`, `--borderRadiusMedium`,
`--fontSizeBase300`, …). `blazorbase-fluent-bridge.css` maps every BlazorBase token onto them, so one palette drives the
whole page: `--color-accent` becomes the brand foreground, stroke and link tokens, `--color-accent-fill` (with its hover and pressed variants) the filled brand backgrounds, the text, surface and border tokens become the neutral
tokens, `--color-success` / `--color-warning` / `--color-danger` become the status tokens, and the radius, spacing and
font tokens become Fluent's. Hover, pressed and selected shades are mixed from the semantic tokens with `color-mix()`,
for example the pressed brand fill from `--color-accent-fill-pressed`, and the foreground on a brand fill is `--color-text-on-accent`. The bridge declares its values on
`body`, which wins over the values Fluent writes on `:root`, whatever the order of the stylesheets. It is the only file
that names Fluent tokens.

The bridge also offers one class for a destructive primary action: `FluentButton Class="base-danger-action"` turns the
brand colours of that button into the danger colours.

There is no theme component. Light and dark come from the tokens (`data-theme` on `<html>`, see below), and
`ThemeService` hands the same preference to Fluent so that the tokens the bridge does not cover (shadows, palettes) follow.
An application that wants another brand colour overrides `--color-accent`, the accent fill tokens and the tokens next to them — nothing else.

### Applying and switching

`IThemeService` owns the preference. It is deliberately split so that nothing touches the document
before the first render:

| Member | Purpose |
|---|---|
| `InitializeAsync()` | Reads the preference. No DOM access, safe from `OnInitializedAsync`. |
| `ApplyAsync()` | Writes it onto `<html>`. Uses JS interop — call it after the first render. |
| `SetThemeAsync(preference)` | Sets, applies, persists, and raises `ThemeChanged`. |
| `CurrentTheme` | `System`, `Light` or `Dark`. |
| `AccentColor` / `NeutralBaseColor` | The resolved `--color-accent` and `--color-neutral-base`, for code that needs the colour on the managed side. Null until `ApplyAsync` has run. |

`BaseLayout` already does both calls. Hosting it is all an application needs; `BaseThemeToggle` gives
the user a control that cycles system → light → dark. Register the Fluent services with
`services.AddBlazorBaseFluentUI()` instead of `AddFluentUIComponents()` — it sets the defaults the layout is built for
(at most three toasts, at the top end of the window below the header, fields without Fluent's default 12px vertical margin) and takes an optional callback for further
`LibraryConfiguration` changes. Register the BlazorBase services with `services.AddBlazorBaseComponents()` —
`AddBlazorBaseUserClient()` already calls it, so an application on the user stack needs nothing extra.

`IThemeService` (BlazorBase) shares its name with Fluent's own `IThemeService`. A file that imports both namespaces names the
one it means: `using IThemeService = BlazorBase.Components.Services.IThemeService;`.

Where the preference comes from: the signed-in user's profile claim seeds a browser that has no choice
of its own, so the setting follows the user to a new device. An explicit choice made here wins from
then on, which is also what makes it survive a reload. Only `SetThemeAsync` writes to local storage.

A choice made while signed in is also stored on the account, so it follows the user to a new browser:
after applying it, `SetThemeAsync` calls the registered `IThemePreferenceSync`. `AddBlazorBaseUserClient()`
registers `UserThemePreferenceSync`, which sends `PUT api/user/me/settings` (it reads the profile first,
because the endpoint replaces theme and language together). It does nothing for an anonymous visitor, skips
the request when the account already holds the value, and swallows a failed request, so an offline user
keeps the local-only behavior. `SetThemeAsync(preference, persistToAccount: false)` skips the account
write for a caller that saves the whole profile itself, as `UserPreferencesPanel` does. An application
without the user package registers nothing and stays local-only.

While the preference is `System`, a `matchMedia` listener watches the operating system and re-reads the
tokens when it flips. The stylesheet repaints on its own — what needs the listener is everything derived
from the tokens on the managed side: `AccentColor` / `NeutralBaseColor` and the `theme-color` meta tag, which
would otherwise keep the values of the theme that just went away. An explicit choice stamps the root
element and is unaffected.

### The three states

An explicit choice stamps `data-theme="light"` or `data-theme="dark"` on the root element. The default
`System` preference stamps nothing and lets `prefers-color-scheme` decide. The stylesheet is built for
all three: the light palette sits on bare `:root`, and the dark values are declared twice — once behind
the media query, guarded so an explicit light choice still beats a dark operating system, and once
behind the stamp.

### Tokens

| Token | Used for |
|---|---|
| `--color-bg-surface` | Page background |
| `--color-bg-elevated` | Cards, dialogs, the data-grid surface, the mobile overflow sheet |
| `--color-bg-subtle` | A surface recessed against the one it sits on: toolbars, code blocks, the diff gutter |
| `--color-bg-hover` | Hover on rows and menu entries |
| `--color-bg-selected` | Selected row, active mobile tab |
| `--color-border` | Borders, dividers, grid lines |
| `--color-border-strong` | Emphasised edges, the sheet handle |
| `--color-border-focus` | Keyboard focus ring |
| `--color-text-primary` | Body text, headings, cell values |
| `--color-text-secondary` | Secondary labels, column headers, icons |
| `--color-text-muted` | Placeholders, disabled state, empty state |
| `--color-accent` | The accent as a colour on the page: focus and selection borders, indicators, icons, accent lines. Not a surface for text — use `--color-accent-fill` for that |
| `--color-accent-fill` / `--color-accent-fill-hover` / `--color-accent-fill-pressed` | The accent as a surface that carries a label: primary button, selected row, badge, chat bubble. Light theme `#2DD4BF` / `#14B8A6` / `#0D9488`, dark theme `#0F766E` / `#115E59` / `#134E4A` |
| `--color-accent-text` | The accent as text or an icon on a surface: links, the active label (darkened in light, lightened in dark until it reads) |
| `--color-accent-hover` / `--color-accent-pressed` / `--color-accent-subtle` | Shades mixed from the accent: hovered and pressed fill, a light tint |
| `--color-text-on-accent` | Text and glyphs on `--color-accent-fill` and its variants: near-black `#07100F` in the light theme (10.2:1 at rest, 5.1:1 pressed), white in the dark theme (5.5:1 at rest, 7.6:1 hover, 9.5:1 pressed). Never paint it on `--color-accent` |
| `--color-neutral-base` | A grey in the hue of the palette; disabled and skeleton colours are tinted from it |
| `--color-success` / `--color-warning` | Confirmations and warnings |
| `--color-danger` / `--color-danger-bg` / `--color-text-on-danger` | Delete and validation errors |
| `--space-0` … `--space-6` | Spacing scale: 2, 4, 8, 12, 16, 24, 32 px |
| `--radius-xs` / `-sm` / `-md` / `-lg` / `-pill` | Corner radii: 4, 6, 10, 16 px, fully round |
| `--font-base` / `--font-display` / `--font-monospace` | Type stacks |
| `--font-size-xxs` … `--font-size-hero` | 10, 12, 14, 16, 20, 24, 28, 32 px (`xxs`, `xs`, `base`, `md`, `lg`, `xl`, `xxl`, `hero`) |
| `--line-height-xxs` … `--line-height-hero` | 14, 16, 20, 22, 28, 32, 36, 40 px, same steps |
| `--font-weight-regular` / `-medium` / `-semibold` / `-bold` | 400, 500, 600, 700 |
| `--control-height-sm` / `-md` / `-lg` / `-touch` | 24, 32, 40, 44 px: control heights and touch targets |
| `--stroke-width-thin` / `-thick` | 1 px borders, 2 px focus rings |
| `--shadow-sm` / `-md` / `-lg` / `-xl` | Elevation: cards, menus, panels, dialogs |
| `--duration-fast` / `-normal` / `-slow`, `--easing-standard` | Transitions |

Four utility classes sit next to the tokens: `base-touch-target` gives a checkbox, switch or radio the control height
(the touch height on coarse pointers), `base-choice` puts a space between a checkbox or radio and its label and sets the
label in the primary text colour (give it to every `FluentCheckbox` and `FluentRadio`), `base-prominent-button` sets a subtle
button in the primary instead of the secondary text colour, and `base-danger-action` (in the bridge) makes a button's accent colours danger colours.

### Customising

Override the tokens in a stylesheet linked **after** the default one. Swapping the brand colour takes
three to six of them — the accent, its selection tint, the fill set (`--color-accent-fill`, `-hover`, `-pressed`, and `--color-text-on-accent` when the fill is too light or too dark for the default label) and, when the greys of the palette change hue, the neutral base:

```css
:root {
    --color-accent: #6D28D9;
    --color-accent-fill: #6D28D9;
    --color-accent-fill-hover: #5B21B6;
    --color-accent-fill-pressed: #4C1D95;
    --color-text-on-accent: #FFFFFF;
    --color-bg-selected: #6D28D914;
    --color-neutral-base: #878289;
}

@media (prefers-color-scheme: dark) {
    :root:not([data-theme="light"]) {
        --color-accent: #A78BFA;
        --color-accent-fill: #6D28D9;
        --color-accent-fill-hover: #5B21B6;
        --color-accent-fill-pressed: #4C1D95;
        --color-text-on-accent: #FFFFFF;
        --color-bg-selected: #A78BFA22;
        --color-neutral-base: #878289;
    }
}

:root[data-theme="dark"] {
    --color-accent: #A78BFA;
    --color-accent-fill: #6D28D9;
    --color-accent-fill-hover: #5B21B6;
    --color-accent-fill-pressed: #4C1D95;
    --color-text-on-accent: #FFFFFF;
    --color-bg-selected: #A78BFA22;
    --color-neutral-base: #878289;
}
```

The text, hover and pressed shades of the accent (`--color-accent-text`, `-hover`, `-pressed`, `-subtle`) follow `--color-accent` on their own; the fill set does not, because a fill is chosen for the contrast of its label, not derived from the accent. Keep a label contrast of at least 4.5:1 on every state of a fill. In a dark theme prefer white labels on a deep fill over black labels on a bright one. Everything else is neutral scaffolding.

---

## Layout and navigation

`BaseLayout` renders the header, a navigation slot and the body, and wires the theme and language
services. One `NavigationItem` tree drives both presentations, so desktop and mobile cannot drift
apart:

- `BaseSideNavigation` — the desktop rail, built on `FluentNav`: entries are `FluentNavItem`s, groups are collapsible
  `FluentNavCategory`s that remember in the browser whether they are expanded (the group holding the current page opens
  itself; `NavigationItem.ExpandedByDefault` opens a group until the user has chosen). The selected page is marked by a
  background and a bar over the full height of its row; the group holding it only turns accent-coloured. The rail fills the width of the container it is placed in (`width: 100%`), so a host sidebar of any width, 240 px for example, is used completely instead of being clipped; reduced to icons it is 48 px wide. Reduced to icons, a group is one icon button that shows or hides the icons of its entries.
- `BaseBottomNavigation` — the mobile tab bar. Entries flagged `IsMobilePrimary` become tabs; the rest
  land behind a "More" bottom sheet that also handles groups (with a back step) and actions such as
  logging out.

`NavigationVisibility` filters the tree once against the current principal, dropping entries whose
`RequiredRole` does not match and groups left with no visible children.

`NavigationItem.BottomBarOnly` marks an entry that exists for phones only, e.g. a fallback "Alerts" tab. It
is never listed in `BaseSideNavigation` on wide screens (a group left without children is dropped there). On
a phone it is a bottom tab when `IsMobilePrimary` selects it. If it does not make it into the bar (more
flagged entries than `MaxBottomBarItems`), it is listed in "More": in the built-in sheet of
`BaseBottomNavigation`, and — because `BaseLayout` lets the "More" tab open the navigation drawer — in the
drawer's `BaseSideNavigation` while the bottom bar is shown (`NavigationLayoutContext.BottomBarMaxItems`).
The drawer assumes the side navigation and `BottomNavigationItems` are driven by the same list.

```csharp
private static readonly IReadOnlyList<NavigationItem> Items =
[
    new() { Href = "/", Label = "Home", MatchAll = true, IsMobilePrimary = true, Icon = new Size20.Home() },
    new() { Href = "/notes", Label = "Notes", IsMobilePrimary = true, Icon = new Size20.Notepad() },
    new() { Label = "Administration", RequiredRole = "Admin", Icon = new Size20.Settings(),
            Children = [ new() { Href = "/admin/users", Label = "Users" } ] },
    new() { Label = "Sign out", IsDanger = true, OnClick = SignOutAsync },
];
```

The host positions and shows or hides the bar — typically fixed to the bottom below a breakpoint. The
component owns the appearance of the tabs and the sheet, not where the bar lives.

> The sheet renders as a sibling of the bar so its backdrop covers the viewport. Do not place the
> component inside a `position: fixed` or transformed wrapper.

### BaseLayout parameters and behavior

| Parameter | Purpose |
|---|---|
| `AppTitle` | Plain text at the start of the header. |
| `Brand` | A `RenderFragment` that replaces `AppTitle`, for example a logo image. The brand is also what stays visible on a phone, where `AppTitle` is hidden. Give an image an `alt` text. |
| `AppSwitcher` | A `RenderFragment`, normally a `BaseAppSwitcher`, rendered right after the navigation button. It replaces `Brand` and `AppTitle` (neither is rendered) and left-aligns the header; without it the header is unchanged. See [App switcher](#app-switcher). |
| `AppLinks`, `CurrentAppKey` | The `AppLinkGroup` list and the key of the current app, **passed once**: the layout shows a default `BaseAppSwitcher` for them when `AppSwitcher` is not set, a `BaseAppSwitcher` inside `AppSwitcher` that has no `Groups` of its own uses them too, and the command palette offers them under "Apps". See [App switcher](#app-switcher) and [Command palette](#command-palette). |
| `ShowCommandPalette` | Default `false`. Shows the search field in the center of the header (a search button on a phone) and the command palette it opens; the brand then sits next to the navigation button. See [Command palette](#command-palette). |
| `NavigationItems` | The navigation entries the command palette lists as pages (falls back to `BottomNavigationItems`). Unlike `BottomNavigationItems` it shows no bottom bar. |
| `Navigation` | The navigation content, usually `BaseSideNavigation`. Without it the header shows no navigation button. |
| `HeaderContent` | Controls on the right of the header, in front of the built-in buttons (for example a notification bell). |
| `HeaderEndContent` | Controls rendered after the built-in buttons, at the very end of the header (for example sign out). |
| `Banner` | Content shown above the page, inside the scrolling main area. |
| `ShowHelp` | Default `false`. Shows a "?" button in front of the theme toggle and the contextual help panel it opens — see [Contextual help](#contextual-help). |
| `ChildContent` | The routed page. |

**Responsive behavior.** The shell fills the dynamic viewport (`100dvh`, so a phone's collapsing address
bar does not cut it off) and only the `<main>` area scrolls; the header stays put. The header height is
the `--base-header-height` custom property on `.layout-root` (56 px). Page css that needs the room left
for content should use `height: 100%` or `calc(100dvh - var(--base-header-height))`, not `100vh` minus a
guess, otherwise the page scrolls although its content fits.

| Viewport width | Navigation | Header |
|---|---|---|
| above 1024 px | Persistent rail; the header button collapses it to icons. | Everything visible. |
| 1024 px and below | Off-canvas drawer opened by the same button. It closes on navigation, Escape, a click on the overlay and when the viewport grows past the breakpoint. Focus moves into the drawer on open and back to the button on close; the page behind it is `inert`. | Everything visible. |
| 768 px and below | Same drawer. | `AppTitle` is hidden (a `Brand` stays), padding shrinks, the body padding drops to 12 px. |
| coarse pointer | Header controls grow from 40 px to 44 px (`--base-header-control-size`). | |

**Header order.** From left to right the end of the header shows the search button of the command palette
(`ShowCommandPalette`, phones only), `HeaderContent`, the help button
(`ShowHelp`), the theme toggle (`ShowThemeToggle`), the settings button (`ShowSettings`) and finally
`HeaderEndContent`.

**Plain header icons.** The icon buttons in the header — the navigation button, help, theme toggle, settings,
the header actions and whatever a host puts into `HeaderContent` / `HeaderEndContent` — are `FluentButton`s with
`Appearance="ButtonAppearance.Transparent"`, `IconOnly="true"` and an `IconStart` icon, and carry the class
`base-header-control`. That class gives them the header control size (40 px, 44 px on touch screens) and a rounded
hit area, and `--color-bg-selected` while their popup is open (`aria-expanded="true"`); the transparent appearance
draws the muted text color, `--color-bg-hover` on hover and the focus ring. A host button in the header sets
`Class="base-header-control"` to match; the unread badge of the notification bell is not affected.

### App switcher

`BaseAppSwitcher` (namespace `BlazorBase.Components.Layout.AppSwitcher`) is a website switcher for the header: a
chip with the icon and name of the current app and a chevron, which opens a popover with grouped links to the
other apps and websites. Pass it to `BaseLayout.AppSwitcher`; the chip then sits next to the navigation button
and replaces the brand and the title.

```razor
<BaseLayout AppTitle="Notes" ShowHelp="true">
    <AppSwitcher>
        <BaseAppSwitcher Groups="Groups" CurrentKey="notes" />
    </AppSwitcher>
    <ChildContent>@Body</ChildContent>
</BaseLayout>
```

```csharp
private static readonly IReadOnlyList<AppLinkGroup> Groups =
[
    new()
    {
        Key = "apps", Label = "My apps",
        Links =
        [
            new() { Key = "notes", Label = "Notes", Href = "https://notes.example/", IconSrc = "images/apps/notes.svg" },
            new() { Key = "tasks", Label = "Tasks", Href = "https://tasks.example/", IconSrc = "images/apps/tasks.svg" },
        ],
    },
    new()
    {
        Key = "internal", Label = "Internal", RequiredRole = "Employee",
        Links = [ new() { Key = "wiki", Label = "Wiki", Href = "https://wiki.example/", OpenInNewTab = false } ],
    },
];
```

| Type / parameter | Purpose |
|---|---|
| `AppLink` | One entry: `Key`, `Label`, `Href` (required); `IconSrc`, `Description` (second line), `RequiredRole`, `OpenInNewTab` (default `true`). |
| `AppLinkGroup` | A titled group: `Key`, `Label` (required), `Links`, `RequiredRole`. |
| `Groups` | The groups to offer. |
| `CurrentKey` | The `AppLink.Key` of the app the user is in. That link is marked (accent ring, small check, `aria-current="true"`), and its label and icon fill the chip. |
| `CurrentLabel`, `CurrentIconSrc` | Override the chip's name and icon; useful when the current app is not in the list. |
| `AriaLabel` | Accessible name of the chip; defaults to a localized "<current>, switch website". |

**Role filtering.** `RequiredRole` on a link or group is a comma separated list of which the user needs at least
one, evaluated against the cascading `Task<AuthenticationState>` exactly like `NavigationItem.RequiredRole`. Hidden
links are never rendered, and a group without visible links disappears. If nothing is visible the chip is shown as
a plain, non-interactive label (or not at all without a current label).

**Behavior.** The popover is a plain `role="dialog"` region of real `<a>` elements (no `FluentMenu`, so it works
without a menu provider and is bUnit-testable); it is only in the DOM while open. Links open in a new tab with
`rel="noopener noreferrer"` unless `OpenInNewTab` is `false`. The popover closes on Escape, on a pointer press or
focus outside, on the chip and after a link was chosen; focus then returns to the chip (not after a click
elsewhere). On open, focus moves to the current link; arrow keys, Home and End move between the links. The
outside/Escape/arrow handling is a small WASM-safe module, `_content/BlazorBase.Components/js/appSwitcher.js`,
attached only while the popover is open and detached on close and dispose.

| Viewport width | Chip | Popover |
|---|---|---|
| above 768 px | Icon, name, chevron (rotates while open). | Anchored below the chip, up to 600 px wide, two columns of balanced height, scrolls when the viewport is short. |
| 768 px and below | Icon only. | As above; from 720 px down it spans the viewport width. |
| 640 px and below | Icon only. | Bottom sheet: slides up with a dimmed backdrop and a grab handle, up to 86 % of the viewport high, links as larger tiles in two columns (one below 360 px). |

All strings (`SwitchWebsite`, `SwitchWebsiteFrom`, `PopoverLabel`) are localized in English and German.

**Passing the links once.** Instead of a `BaseAppSwitcher` with its own `Groups`, give the layout
`AppLinks` and `CurrentAppKey`:

```razor
<BaseLayout AppTitle="Notes" AppLinks="Groups" CurrentAppKey="notes" ShowCommandPalette="true" ...>
```

The layout then shows a default switcher, a `BaseAppSwitcher` placed in `AppSwitcher` without `Groups` or
`CurrentKey` takes both from the layout (an explicit `Groups` or `CurrentKey` wins), and the command palette
lists the same links under "Apps". A switcher with its own `Groups` and no `AppLinks` on the layout still works
as before, but the palette does not know its links.

### Command palette

`BaseCommandPalette` (namespace `BlazorBase.Components.CommandPalette`) is a generic "go anywhere, do anything"
search: a centered panel that Ctrl+K opens, with grouped results for pages, apps and actions. The framework
owns the UI, the ranking and the permission gate; a site only feeds it through providers. Turn it on with
`BaseLayout.ShowCommandPalette`; without it nothing changes.

```razor
<BaseLayout AppTitle="Notes"
            ShowCommandPalette="true"
            NavigationItems="Items"
            AppLinks="AppLinkGroups"
            CurrentAppKey="notes">
```

With that, the palette already offers every page of `Items` ("Pages", the breadcrumb as subtitle, for example
"GitHub Copilot › Budgets", entries with `NavigationItem.OnClick` as "Actions") and every app link ("Apps", the
group title as subtitle). `NavigationItems` is optional: the palette uses `BottomNavigationItems` when it is not
set, so a site that already passes the list for the bottom bar passes nothing more. These built-in providers are
`NavigationCommandProvider` and `AppLinkCommandProvider`; both are public and can also be registered on their own
(`services.AddCommandPaletteProvider(sp => new NavigationCommandProvider(items, localizer))`).

**Header.** The search field is a button in the center of the header: a search icon, "Search pages, actions and
apps…" and a `Ctrl K` hint. At 768 px and below it collapses into a plain search icon button at the start of the
header's end area, and the palette becomes a full screen sheet. With the field shown the brand (or the title) sits
next to the navigation button, exactly like with the app switcher.

**Registration.** `AddBlazorBaseComponents()` already registers the palette (`AddBlazorBaseCommandPalette()`, safe
on Blazor Server and WebAssembly; call it again with an options delegate to tune it). Add the providers of the
site:

```csharp
services.AddCommandPaletteProvider<ProjectSearchProvider>();
services.AddCommandPaletteProvider((request, ct) => ValueTask.FromResult<IReadOnlyList<CommandPaletteEntry>>(
[
    new() { Id = "action:new-note", Title = "New note", Group = "Actions", GroupOrder = 300, Execute = CreateNoteAsync },
]));
services.AddCommandPaletteProvider([new CommandPaletteEntry { Id = "help", Title = "Help", Group = "Pages", Href = "/help" }]);
```

| Registration | Use |
|---|---|
| `AddCommandPaletteProvider<T>()` | A class implementing `ICommandPaletteProvider`, created per scope (circuit or browser); may use injected services. |
| `AddCommandPaletteProvider(factory)` | A provider built by a `Func<IServiceProvider, ICommandPaletteProvider>`. |
| `AddCommandPaletteProvider(search)` | One function of `(CommandPaletteRequest, CancellationToken)`. |
| `AddCommandPaletteProvider(entries)` | A fixed list of entries. |

**Provider contract.** `ICommandPaletteProvider.GetEntriesAsync(CommandPaletteRequest, CancellationToken)` receives the
trimmed `Query`, the `User` (an anonymous principal when nobody is signed in), the `Culture` and the `MaxResults` hint.
An empty query means the palette has just opened: return the entries to suggest, preferably all of them, because the
palette ranks, filters and caps the result itself. A provider can be a synchronous in-memory list or a server backed
search; it must honor the token (the palette cancels the call when the user types on). A provider that throws or
does not answer within `ProviderTimeout` (5 s) is logged and skipped; the others still show.

`CommandPaletteEntry`:

| Member | Meaning |
|---|---|
| `Id` | Required, unique across all providers (the first entry wins); it is the key of the recent list. |
| `Title`, `Subtitle` | The row texts, already localized. The subtitle is searched with a lower priority. |
| `Group`, `GroupOrder` | The already localized group heading and its position (lower first, then by name). |
| `Order` | Optional `int?`, position within the group: lower values first; entries without a value follow in the order the providers delivered them, ties keep the delivery order (stable sort). `NavigationCommandProvider` numbers its entries in the depth-first order of the tree, `AppLinkCommandProvider` in the order of groups and links. |
| `Href` / `Execute` | **Exactly one.** `Href`: a relative address or an absolute one below the application is navigated to; any other absolute `http`, `https`, `mailto` or `tel` address opens in a new tab (`noopener noreferrer`); other schemes (such as `javascript:`) are never opened. `Execute`: awaited; an exception is caught, logged and shown as a toast. An entry with neither or both is dropped and logged. |
| `OpenInNewTab` | Opens `Href` in a new tab even when it points into the application. |
| `Icon` / `IconSrc` | A Fluent icon or an image for the tile; a default glyph by kind otherwise. |
| `Keywords` | Extra search terms that are not shown. |
| `Hint` | The already localized kind at the end of the row; defaults to "Action" (`Execute`), "Link" (absolute `Href`) or "Page". |
| `RequiredRole` | See the permission rules below. |
| `PreMatched` | The provider matched the entry against the query itself (fuzzy or full text search); it is listed even when the query does not occur in its texts and ranks below text matches. |

**Permission rules.** The palette gates centrally: after collecting, it drops every entry whose `RequiredRole`
(comma separated, at least one role needed, evaluated like `NavigationItem.RequiredRole`) the current user does
not have, **whatever provider returned it**. An unauthenticated user therefore only gets entries without a role.
Finer checks (authorization policies, per record permissions, tenants) are the provider's job: it must never return
an entry the user may not use. The built-in providers additionally filter the navigation tree and the link groups
exactly like the side navigation and the app switcher do (a role on a parent hides its children).

**Ranking.** Matching is case and diacritic insensitive over `Title`, `Keywords` and `Subtitle`; every word of
the query must match. A word scores by its best match, strongest first: title starts with, keyword starts with, word
in the title starts with, word in a keyword starts with, title contains, keyword contains, subtitle starts with,
a word of the subtitle starts with, subtitle contains; the entry takes the average over its words, plus a bonus
for a title equal to the query. Equal scores list the lower `Order` first (entries without one last), then the
shorter title, then alphabetically. Groups keep
their stable order (`GroupOrder`, then name); while typing at most 6 results per group and 30 in total are shown,
while the query is empty 5 suggestions per group, taken after sorting the group by `Order` (so the first five
pages of the navigation, extra entries of other providers after them). Recently used entries (the last 5 ids, stored in the browser's
`localStorage` per user name) are listed first under "Recent" when the query is empty — only while a provider still
returns them for the empty query — and ranked higher while typing. The providers are asked 120 ms after the last
keystroke, a stale search is cancelled, and a loading indicator appears only when a search takes longer than 300 ms.
All numbers are `CommandPaletteOptions` (`MaxResults`, `MaxResultsPerGroup`, `MaxSuggestionsPerGroup`, `MaxRecent`,
`SearchDebounce`, `ProviderTimeout`, `LoadingIndicatorDelay`, `SlashOpensPalette`).

**Opening from code.** Inject `ICommandPaletteService` (scoped) and call `OpenAsync(string? initialQuery = null)`
or `CloseAsync()`; `IsOpen`, `InitialQuery` and the `Changed` event expose the state.

```razor
@inject ICommandPaletteService Palette

<FluentButton OnClick="@(() => Palette.OpenAsync("budget"))">Find budgets</FluentButton>
```

**Keyboard.**

| Key | Action |
|---|---|
| Ctrl+K or Cmd+K | Opens the palette from anywhere, or closes it again. |
| `/` | Opens the palette while no input, text area, select or editable element has the focus (switch off with `SlashOpensPalette`). |
| Up / Down | Moves the selection, wrapping around. |
| Home / End | Jumps to the first / last result while the field is empty, otherwise with Ctrl (so the caret still works in the text). |
| Enter | Runs the selected entry; the palette closes first. |
| Esc | Closes the palette wherever the focus is (also after a mouse click on a title or an empty spot); the focus returns to the element that had it before. |
| Tab | Stays inside the palette. |

**Markup and accessibility.** The palette is plain elements (no `FluentDialog`), so it is bUnit testable: a
`role="dialog"` with `aria-modal`, a `role="combobox"` input with `aria-controls` and `aria-activedescendant`
pointing at the selected `role="option"` of a `role="listbox"`, group headings as `role="group"` labels, matched text
in `<mark>` (rendered as text, never as HTML) and a polite live region that announces the number of results. A small
WASM-safe module, `_content/BlazorBase.Components/js/commandPalette.js`, only provides the global shortcut, the Escape
key on the whole page while the palette is shown, the Tab trap, keeping the focus in the input when a non-interactive
spot of the panel is pressed (the panel itself has `tabindex="-1"` as a safety net), focus restore, scroll into view,
the recent list in `localStorage` and the new tab for external addresses. On
a phone (768 px and below) the palette fills the screen with 56 px rows and a close button instead of the key hint.
Its strings are localized in English and German (`BaseCommandPalette.razor.resx`, `BaseLayout.razor.resx`).

### Header helper classes and services

**Helper classes.** Two classes let a host move controls that do not fit a phone header into the
drawer without writing its own media queries:

- `base-header-wide-only` — put it on an element inside `HeaderContent` or `HeaderEndContent`; it is hidden at 768 px and below.
- `base-nav-narrow-only` — put it on an element inside `Navigation`; it is hidden on wide screens and
  shown at 768 px and below, at the bottom of the drawer.

```razor
<BaseLayout AppTitle="Notes">
    <Brand><img src="images/logo.svg" alt="Notes" height="28" /></Brand>
    <HeaderContent>
        <BaseThemeToggle />
    </HeaderContent>
    <HeaderEndContent>
        <FluentAnchorButton Href="/account" Appearance="ButtonAppearance.Transparent" Class="base-header-wide-only base-header-control">Account</FluentAnchorButton>
    </HeaderEndContent>
    <Navigation>
        <BaseSideNavigation Items="Items" />
        <div class="base-nav-narrow-only"><a href="/account">Account</a></div>
    </Navigation>
    <ChildContent>@Body</ChildContent>
</BaseLayout>
```

**Services.** `BaseLayout` resolves `IThemeService` and `ILanguageService` (registered by
`AddBlazorBaseComponents()`), and, since the shell rework, also `IJSRuntime` (it loads
`_content/BlazorBase.Components/js/layout.js` to watch the breakpoint) and `NavigationManager` (to close
the drawer when the location changes). A bUnit test of a page that hosts the layout therefore needs the
JS interop set to loose mode or the `layout.js` module set up. Navigation placed in the `Navigation`
slot receives a cascading `NavigationLayoutContext` (collapsed state, a function that closes the
drawer, and — while the bottom bar is shown — its tab capacity), which `BaseSideNavigation` uses.
---

## Contextual help

`BaseLayout` can show a page-specific help panel. The content is ordinary Markdown that lives in the host
repository and is **single-sourced**: the same file is a page of the DocFX documentation site and the text
the panel shows, so it is written and maintained once.

### Wiring

```csharp
builder.Services.AddBlazorBaseHelp(options =>
{
    options.Assemblies.Add(typeof(App).Assembly);
    options.CultureFolders["en"] = "en-US";
    options.CultureFolders["de"] = "de-DE";
    options.DocumentationBaseUrl = "https://docs.example.com/{culture}/";
});
```

```razor
<BaseLayout AppTitle="Notes" ShowHelp="true"> ... </BaseLayout>
```

```csharp
@attribute [HelpTopic("general/blog")]
```

`[HelpTopic]` goes on a routable component (one that has `@page`/`[Route]`). The topic is the path of a
Markdown file below the culture folder without `.md`; `general/blog` is read from
`{ContentBasePath}/{culture}/general/blog.md`. A topic is usually `<category>/<page>`, where the category folder
follows the sidebar group of the page (`general`, `administration`, ...). A page without the attribute, or whose
topic has no file, shows `FallbackTopic`. `AddBlazorBaseHelp` is additive-safe and registers `HelpOptions`,
`IHelpTopicResolver` (singleton), `IHelpContentSource` (scoped) and `IHelpFieldIndex` (scoped, backs the field
tooltips).

| `HelpOptions` | Default | Purpose |
|---|---|---|
| `Assemblies` | empty | Assemblies scanned for routable components with `[HelpTopic]`. Pages must be public types, as the router requires. |
| `ContentBasePath` | `help` | Where the content is served from, relative to the application's base address. |
| `DefaultCulture` | `en-US` | Culture folder tried when the content is missing in the current culture. |
| `FallbackTopic` | `getting-started` | Topic for pages without a topic and for topics without content. |
| `CultureFolders` | empty | Maps a UI culture (`en`, `de-AT`) to the folder name (`en-US`). Lookup order: full culture name, language, the name itself. |
| `DocumentationBaseUrl` | `null` | Address of the DocFX site. Topics are appended with `.html`; `{culture}` is replaced by the culture folder. Null hides the "Open full documentation" link and turns links beyond the help content into plain text. |
| `ShowFieldTooltips` | `true` | Shows the [field tooltips](#table-of-contents-anchors-and-field-tooltips) on labels of the page. `false` renders nothing and registers no listener in the browser. |

### How the panel behaves

- The "?" button toggles the panel. From 1200 px the panel is **docked**: it takes its own column (360–420 px)
  next to the page, which shrinks, so the user can read and work at the same time. Below 1200 px it is a modal
  sheet with a dimmed backdrop and the page behind it `inert`. Opening the help closes the settings panel and the
  navigation drawer, and opening the settings closes the help.
- Focus moves into the panel on open and back to the "?" button on close; Escape and the close button close it.
  A docked panel does not trap Tab; the sheet does.
- It follows navigation while it is open and reloads only when the page changes (not for query-string changes).
  While loading it shows a status, and *"No help is available for this page yet."* when no content exists at all.
- The header shows the title (front matter `title`, else the first `# H1`) and, with `DocumentationBaseUrl`, the
  link to the same topic on the documentation site (new tab).
- A link in the Markdown to another topic **without a page** (a category topic such as
  `../concepts/budgets-and-boosts.md`) opens **inside the panel**. The topic shown before is pushed on a history
  stack and a back button appears at the start of the header; it returns to the previous topic and disappears when
  the stack is empty. The title, the body and the documentation link follow the shown topic. The stack is cleared
  when the panel closes and when the user navigates to another page (a change of the query string does not).
  A link to a topic **with** a page keeps navigating to that page, with the panel open. A topic that does not exist
  shows the "no help" text instead of the fallback topic, and back still works.
- `BaseHelpPanel` can also be used without `BaseLayout`; it needs `IsOpen`, `IsDocked` and `OnClose`.

### Table of contents, anchors and field tooltips

**Table of contents.** Below the header the panel shows a bar with a **Contents** (de: *Inhalt*) button, outside the
scrolling content, so it stays visible while the user reads. The button shows the current section (scroll spy:
the last heading at or above the top of the content) and toggles a nested list of the `##` and `###` headings of
the shown topic — a `###` belongs to the `##` above it. Selecting an entry scrolls to the heading and closes the
list (on the docked panel and on the phone sheet alike); Escape closes the list before it closes the panel. The
bar is hidden when the topic has fewer than two such headings. It follows the shown topic, so it works with the
topic history (back button), in light and dark theme and on the phone sheet (the list is an overlay inside the panel).

**Anchors.** Every rendered heading gets an `id` that is its slug (`HelpSlug`: lower case, hyphens, `ä ö ü ß` to
`ae oe ue ss`, other accents removed, unique per document with `-2`, `-3`, … — de-DE and en-US headings give
different ids, so link anchors per language). The panel can open a topic at an anchor, scrolls to the element and highlights it briefly
(`help-anchor-highlight`, no animation with reduced motion):

- `BaseHelpPanel` parameters `AnchorTopic`, `Anchor` and `AnchorRequest` (a counter: every change asks for the
  anchor once, also when it is the same as before). When the shown topic is not `AnchorTopic` the panel shows that topic
  first (the topic shown before goes to the history stack).
- `[JSInvokable("OpenTopicAtAnchor")] OpenTopicAtAnchorAsync(topic, anchor)` does the same from script.
  `OpenTopicAsync(topic)` stays as it was.
- `HelpFieldTooltips.OnOpenHelp` (`EventCallback<HelpAnchorTarget>`) is what `BaseLayout` uses to open the panel (and
  close the settings panel and the navigation drawer) at a field entry.

**Fields section.** A help page can list the labels of its UI after the marker `<!-- help:fields -->`:

```markdown
## Fields
<!-- help:fields -->
### Post
- **Title**: The headline of the post. Shown in lists and in the feed.
- **Status**: Draft or published. Only published posts are visible to readers.
## See also
```

The field entries are the top-level list items between the marker and the next `##` heading (`###` group headings
are fine) whose first paragraph starts with a bold label; the label may carry the colon inside or outside the bold
text. `HelpMarkdownProcessor` removes the marker from the Markdown and keeps its line in
`HelpDocument.FieldsStartLine` (a marker inside a Docs-Only block or an unreadable file is gone, so the page has no
entries); `HelpStructureReader.Read(document)` returns `HelpDocumentStructure` with every `Headings` entry,
the nested `TableOfContents` and the `Fields`. A field entry is rendered as `<li id="field-{slug of label}" class="help-field">`
(unique in the document, together with the heading ids), which is what an anchor can point at.
The short text of an entry is its **first sentence**: up to a `.`, `!` or `?` followed by white space and an
upper-case letter (or the end); the whole entry text when there is none. Write short first sentences without
abbreviations that end in a period followed by a capital. It is rendered as safe inline HTML — bold, italic, strikethrough and
code are kept, links become plain text, everything is HTML-encoded.

**Field tooltips.** `BaseLayout` (with `ShowHelp`, `HelpOptions.ShowFieldTooltips` on and `IHelpFieldIndex`
registered — `AddBlazorBaseHelp` does that) renders the component `HelpFieldTooltips`, which loads `helpTooltips.js`:

- The script listens (delegated, capturing, passive) on the document, but only reacts inside the main content
  (`#base-main`) and inside dialogs (`.base-dialog`) — never inside the help panel, the header or the navigation.
  It registers nothing when the option is off, and `HelpFieldTooltips` removes every listener and the popover when it is
  disposed (and hides the popover on navigation).
- **How a label is found.** The element under the pointer (or the keyboard focus) is a candidate, and so are its ancestors up
  to three levels. The *visible text* of an element (`innerText`, trimmed, whitespace collapsed, a trailing `:` and `*`
  removed, lower case — `HelpLabelKey.Normalize` does the same on the server) is compared with the label keys of the page's
  field entries. That covers `th` of tables (also with a sort button in it), `label` elements, small label spans and
  headings of cards and tiles. Elements whose text is longer than 80 characters, text inputs, text areas and selects
  are never candidates (a value is not a label); for a focused form control the associated `label`, a slotted
  Fluent `label`, `aria-label` or `label` attribute is used instead.
- **Lazy loading.** The first time the pointer rests on an element with a short text, the script asks the component for the
  index of the current page once (`IHelpFieldIndex.GetForPathAsync`: the topic of the page through `IHelpTopicResolver`,
  its content through `IHelpContentSource`, the fallback topic is *not* used). It is requested again after navigation
  to another path or a culture change; an index that came back empty (no topic, not signed in, no entries) is asked for
  again after 60 seconds at the earliest. Everything after that is matching in the browser, with no further round trips. A user
  who may not read the help content simply gets no tooltips and no error (the content endpoint answers `401` or `403`).
- **The popover** appears after about 400 ms of hovering a matching label, or when a matching element gets keyboard
  focus. It is theme-aware (`--color-*` tokens, rules in `blazorbase.css`), shows the short text and the link **More in help**
  (de: *Mehr in der Hilfe*), is linked to the element with `aria-describedby`, and hides when the pointer leaves, on scroll, resize, click and
  Escape. It never intercepts clicks, sorting or focus of the label. Touch input (`pointerType` touch) shows no tooltip.
  The link opens the panel at the field entry (see anchors above). The popover's link is not part of the page's Tab order;
  keyboard users reach the entry through the help panel and its table of contents.
- A label that appears twice in the field section gets one tooltip (the first entry).

### Topic resolution

The layout never receives the router's route data, so `HelpTopicResolver` scans `HelpOptions.Assemblies`
once for types with a `RouteAttribute` and a `HelpTopicAttribute` and matches the current URL against the route
templates (`HelpRouteMap`). Literal segments match case-insensitively; `{name}`, `{name:constraint}` and
`{name?}` match one segment (or none), `{*name}` the rest of the path. Of several matches the one with most
literal segments wins. The reverse map (topic to parameterless route) turns links to other topics that have a page
into real navigation; topics without a page open in the panel (see above). No change to the host's `Router` is
needed. Under WebAssembly trimming keep the page types (the router needs them anyway).

### File layout

```text
Docu/
  en-US/
    getting-started.md        fallback topic
    general/blog.md           topic general/blog (page topics: <category>/<page>)
    concepts/budgets.md       topic concepts/budgets (no page: opened inside the panel)
    includes/list-basics.md   shared text, [!INCLUDE]d by pages
    images/blog-list.png
    reference/…               documentation-only pages
  de-DE/ …                    same structure
  docfx.json, toc.yml
```

### Serving the content (protected endpoint)

`IHelpContentSource` fetches `{ContentBasePath}/{culture}/{topic}.md` with an `HttpClient`, falls back to the
default culture and then to the fallback topic, and caches every file for the lifetime of the scope. A
Blazor Server host registers `AddHttpClient()` for it. The request is same-origin, so the browser sends the
cookies. A `401`, `403` or `404` answer means "no help" (`401`/`403` are not cached, so the file is asked for
again after the user signed in); a response of type `text/html` counts as "not found" as well, because a host whose
unknown URLs fall back to `index.html` would otherwise answer with the app.
Implement `IHelpContentSource` to read the content from somewhere else.

> **Warning:** never ship the help content as anonymous static web assets (`wwwroot/help`, a static file provider,
> `Content` items that end up in `wwwroot`). It would be readable without signing in, and `<!-- Internal-Start -->`
> blocks would be delivered, too. Serve it with the endpoint below and keep it out of the WebAssembly asset
> manifest and the service worker's precache and cache.

The package `BlazorBase.Components.Server` (ASP.NET Core only, never referenced by a WebAssembly client) maps the
protected endpoint:

```csharp
app.UseAuthentication();
app.UseAuthorization();
app.MapBlazorBaseHelpContent(helpFolder, options =>
{
    options.RequestPath = "/help";
    options.Roles = "Admin,Support";
    options.PolicyName = "HelpReaders";
});
app.MapFallbackToFile("index.html");
```

| `HelpContentEndpointOptions` | Default | Purpose |
|---|---|---|
| `ContentRoot` | required (or the `contentRoot` argument) | Physical folder with one sub-folder per culture. |
| `RequestPath` | `/help` | Must equal `HelpOptions.ContentBasePath` of the client. |
| `PolicyName` | `null` | Authorization policy a signed-in user must also satisfy; failing it answers `403`. |
| `Roles` | `null` | Comma-separated roles of which the user needs one; otherwise `403`. |
| `ExcludedFolders` | `_site`, `obj`, `bin`, `node_modules` | Folder names that are never served (folders starting with `.` never are). |

- `GET {RequestPath}/{culture}/{path}` serves `.md` (`text/markdown; charset=utf-8`) and `.png`, `.jpg`, `.jpeg`,
  `.gif`, `.svg` and `.webp` files; everything else — `docfx.json`, `toc.yml`, other extensions, a missing file —
  is `404`. The culture must look like a culture name. A path that is absolute, contains `..`, `.`, empty
  segments, a backslash, a colon (drive letter, alternate data stream), a percent sign or control characters
  finds nothing, so nothing outside the culture folder is ever read.
- An anonymous request gets **`401`**, not a redirect to a sign-in page, and before the file is looked up, so
  the answer does not reveal which files exist. The endpoint enforces the sign-in itself and is marked
  `AllowAnonymous` only so a fallback policy of the host does not turn the `401` into a challenge. Map it after
  `UseAuthentication`/`UseAuthorization`.
- Markdown is delivered **without its internal blocks**: `HelpInternalContentFilter.Remove` removes
  `<!-- Internal-Start -->` … `<!-- Internal-End -->` (case and spacing tolerant, a superset of the documentation
  pipeline's regex); a start without an end removes the rest of the file. Markdown is sent with
  `Cache-Control: private, no-store`; images with `private, no-cache`, an `ETag` and `Last-Modified`, and SVG
  additionally with a sandboxing `Content-Security-Policy`. Every response carries `X-Content-Type-Options: nosniff`.
- Files are read per request, so pointing `ContentRoot` at the source folder in development shows edits
  without a rebuild; in production point it at a copy that is part of the publish output.
- With a WebAssembly client, keep `/help` out of the service worker: do not precache help files and do not serve
  them from the cache (the published asset manifest must not list them either).

### Single-sourcing with DocFX

| Marker | DocFX site | Help panel |
|---|---|---|
| plain Markdown | shown | shown |
| `<!-- Docs-Only-Start -->` … `<!-- Docs-Only-End -->` (block or inline) | shown | removed |
| `<!-- App-Only` … `-->` (Markdown inside the comment) | invisible, it is a comment | rendered |
| `<!-- Internal-Start -->` … `<!-- Internal-End -->` | hidden in public builds | removed |
| `<!-- help:fields -->` | invisible, it is a comment | removed; starts the [fields section](#table-of-contents-anchors-and-field-tooltips) |

`HelpMarkdownProcessor` (pure, no DI) prepares a file for the panel:

- YAML front matter is removed; `title:` becomes the panel title. Otherwise a first-line `# H1` is the title.
  The H1 itself is left out of the body, because the header shows the title.
- Markers are applied as in the table. A start marker without its end removes the rest of the file; other
  HTML comments are removed.
- `[!INCLUDE [label](relative/path.md)]` is replaced by the file, relative to the including file, at most three
  levels deep; a cycle is skipped. Markers and front matter of the included file are handled too. As in DocFX,
  relative links and images inside an included file are resolved against the *including* page.
- `> [!NOTE]`, `[!TIP]`, `[!IMPORTANT]`, `[!WARNING]` and `[!CAUTION]` become callouts: the quote gets
  `class="help-callout help-callout-{kind}"` and `role="note"` and starts with a
  `<p class="help-callout-title">` carrying the localized title. They are built in the Markdig syntax tree, not by
  passing HTML through.
- Images load from `{ContentBasePath}/{culture}/…`; a relative path that leaves the culture folder, a
  root-relative path and a non-`http(s)` scheme become the alt text.
- A relative link to another `.md` topic whose topic has a page (`[HelpTopic]`) navigates to that page (the panel
  stays open). A relative link to a topic **without** a page becomes an in-panel link: a real anchor
  (`href="#help:{topic}"`, so it stays focusable and keyboard operable) carrying `data-help-topic="{topic}"`, which
  a click handler of the panel (`bindHelpLinks` in `layout.js`) turns into the in-panel navigation; clicks with a
  modifier key or a non-primary button keep the browser's behavior, and a `#fragment` of the link is dropped. The
  link needs no `DocumentationBaseUrl`. Other relative links (non-Markdown files) go to `DocumentationBaseUrl` + the
  path in a new tab, or are plain text without a base URL. `#anchor`-only links are plain text. `http`/`https` links
  open in a new tab with `rel="noopener noreferrer"`, `mailto:` as is, every other scheme becomes plain text.
  Raw HTML stays escaped; `MarkdownLink.DataAttributes` only accepts `data-*` names and HTML-encodes the values.

### Trusted Markdown

`MarkdownConverter.ToHtml(string)` stays as safe as before. The overload
`ToHtml(string, TrustedMarkdownOptions)` is for Markdown that ships with the application, never for user or model
text: `AllowImages`, `OpenExternalLinksInNewTab`, `ResolveImageUrl`, `ResolveLinkUrl`, `AlertTitles` and `PrepareDocument`
(a callback that receives the parsed Markdig document just before it is rendered — the help panel uses it to write heading and
field ids). Raw HTML
is still escaped, URL schemes are still filtered (also the ones a resolver returns), and images only allow
relative or `http(s)` URLs. `MarkdownView` takes the same object as `Options`; with it set the sanitizer seam is
skipped, because the content is trusted.

### Localization and styling

Strings live in `BaseHelpPanel.razor(.de).resx` ("Contents" and "More in help" included) and the "Help" label in `BaseLayout.razor(.de).resx`. The panel's
look follows the settings panel and the `--color-*` tokens, so it works in light and dark themes; callout
colors come from `--color-accent`, `--color-success`, `--color-warning` and `--color-danger`. The tooltip popover and
the anchor highlight are global rules in `css/blazorbase.css` (the popover is created by script, so scoped CSS cannot reach it).

---

## Editors and viewers

| Component | Purpose |
|---|---|
| `RichTextEditor` | Content-editable HTML editor. Sanitises through the host's `IHtmlSanitizer` when one is registered. |
| `SanitizedHtml` | Renders untrusted HTML through the same seam. |
| `MarkdownView` | Renders Markdown (a language model's answer, a comment) as formatted HTML — safe on its own, host sanitizer applied on top. |
| `DiffViewer` | Unified or side-by-side file diff with syntax highlighting and per-line comment anchors. |
| `FileTree` | Collapsible file tree over `FileTreeNode`. |

`IHtmlSanitizer` is a seam, not an implementation, and the two components fall back differently when
no sanitizer is registered. `SanitizedHtml` HTML-encodes the value, so it stays safe and simply shows
the markup as text. `RichTextEditor` passes the value through unchanged, which is correct for content
an author already owns and a real risk for anything else — register a sanitizer before letting one
user's markup reach another user.

`MarkdownView` does not depend on the seam for safety. `MarkdownConverter` (Markdig) escapes raw HTML
instead of passing it through, turns every link whose target is not `http`, `https`, `mailto` or
relative into plain text — including targets obfuscated with control characters or entities — and
renders images as links, so displaying text never makes the browser contact a server the text names.
It enables pipe tables, strikethrough and bare-URL links only; Markdig's `UseAdvancedExtensions()` is
avoided on purpose, because its generic-attributes extension turns `{onclick=…}` into a real attribute.
When an `IHtmlSanitizer` is registered its policy is applied on top, so the host's allow-list has the
last word — which also means it must allow what Markdown produces (`table`, `thead`, `tbody`, `tr`,
`th`, `td`, `pre`, `code`, `hr`, `del` and `class` for the `language-*` of code blocks), or that
structure is stripped. The conversion is cached per value, so re-renders with the same text are free.

---

## Services

| Service | Purpose |
|---|---|
| `IThemeService` | Light/dark/system preference, see above. |
| `ILanguageService` | Current UI language and switching. |
| `IFormFactor` | Reports the host platform and form factor. The seam only; the WebAssembly and MAUI packages each register their own implementation. |
| `IConfirmationService` | Asks the user to confirm an action and reports whether they accepted (`ConfirmAsync`). `ConfirmDestructiveAsync` is the same question for an action that cannot be undone, shown with a warning icon and a danger-styled confirm button; an implementation that does not override it falls back to `ConfirmAsync`. `FluentUiConfirmationService` is the default and needs FluentUI's `IDialogService`. |

`LanguageService` stamps `<html lang>` with the active language on every culture change (startup,
claim-driven resolve, and an explicit `SetLanguageAsync` call), not only `CultureInfo.CurrentUICulture`.
Without it the document stays on whatever the static markup shipped with (typically `en`) for the whole
session, which is wrong for assistive technology and any other tooling that reads the attribute. A
native browser control such as the built-in required-field validation bubble follows the browser's own
UI language instead of the page's `lang` attribute, so this does not and cannot change that one - no web
app can.

`IConfirmationService` lives here because opening a dialog is a UI concern, not a CRUD one — an
application that never touches the data grid can still use it. The convenience registration currently
rides along with `AddBlazorBaseCrudComponents()` in `BlazorBase.CRUD`, which is deliberately separate
from `AddBlazorBaseComponents()`: a pure hosting backend that never renders interactively must not be
made to supply an `IDialogService`. Register it yourself with `TryAddScoped` if you want it without the
CRUD layer.

### Dialogs

Every dialog of the framework and of an application is a component on Fluent's dialog API, opened through
`ShowBaseDialogAsync` (namespace `BlazorBase.Components.Dialogs`), which sets the dialog class `base-dialog`, the default
width and the result model:

```csharp
var result = await DialogService.ShowBaseDialogAsync<MyDialog, MyContent>("Title", new MyContent { ... }, width: "min(440px, calc(100vw - 2rem))");
if (!result.Cancelled)
    var value = result.GetValue<MyResult>();
```

The returned task completes when the dialog is closed — by a button, Escape or a click on the backdrop — and
`DialogResult.Cancelled` tells which. The content component is a `BaseDialogInstance` (or `BaseDialogInstance<TContent>`,
which adds the `Content` parameter) and renders a `BaseDialogBody`:

```razor
@inherits BaseDialogInstance<MyContent>

<BaseDialogBody>
    <ChildContent>
        <FluentText As="TextTag.Paragraph">@Content.Message</FluentText>
    </ChildContent>
    <ActionTemplate>
        <FluentButton Appearance="ButtonAppearance.Primary" OnClick="@ConfirmAsync">OK</FluentButton>
        <FluentButton Appearance="ButtonAppearance.Outline" OnClick="@CancelAsync">Cancel</FluentButton>
    </ActionTemplate>
</BaseDialogBody>
```

| Type | Purpose |
|---|---|
| `BaseDialogOptions` | `Title`, `Width` (any CSS width; default 500 px, never wider than the window), `Size` (a Fluent size, replaces `Width`), `Modal`, `PreventDismissOnEscape`, `ShowCloseButton` with `CloseButtonLabel`, `Class`, `Parameters` (content parameters by property name) and `OnStateChange`. |
| `ShowBaseDialogAsync<TDialog>(options)` / `(configure)` / `<TDialog, TContent>(title, content, width)` | Extensions of `IDialogService`. |
| `BaseDialogInstance` | Base class of the content; gives `DialogInstance` and `ConfirmAsync()`, `ConfirmWithResultAsync(value)` and `CancelAsync()`. Fluent's own footer buttons are off; `UseDefaultFooter` turns them on. |
| `BaseDialogBody` | The frame: the title from the options, the content, an optional footer (`ActionTemplate`, primary button first) and the keyboard behavior below. `FixedHeaderFooter` keeps title and footer in place while the content scrolls (default). `Flush` renders no frame at all: the content fills the dialog and brings its own header and action bar, as a card does. |
| `DialogBehavior` | Used by `BaseDialogBody`: moves focus into the dialog when it opens (to the element marked `data-autofocus`, otherwise the first field) and, with `CommitPendingEditOnEscape`, commits a half-typed field before Escape is handled so an unsaved-changes check sees it. The dialog is a native modal: the page behind is inert, Escape closes it and focus returns to the opener. |

A test cannot replace `IDialogService` or `IDialogInstance` with a mock — their contract has internal members.
It opens the dialog through the real service and a rendered `FluentDialogProvider`, or derives from `DialogService`
to record what was opened.

The dialogs of this package:

- `dialogService.ShowConfirmationDialogAsync(title, new ConfirmationDialogContent { Message = ..., ConfirmButtonText = ..., CancelButtonText = ..., IsDestructive = true })`
  opens `BaseConfirmationDialog`; `IConfirmationService` uses it. A destructive confirmation shows a warning
  icon and a danger-styled confirm button (`base-danger-action`), and starts on **Cancel** so a stray Enter cannot confirm it.
- `dialogService.ShowFileInputDialogAsync(title, new FileInputDialogContent { Message = ..., FileInputCaption = ..., FileFilter = ".xlsx", MaxFileSize = 20 * 1024 * 1024 })`
  opens `BaseFileInputDialog`, a `FluentInputFile` (stream mode) with Confirm/Cancel buttons. The confirm button
  stays disabled until a file both matches `FileFilter` (comma separated extensions, e.g. `.csv,.txt`) and is at
  or under `MaxFileSize` (0 or negative disables the size check); the validation itself lives in the
  dependency-free `FileInputValidator.Validate(...)`, reused here and safe to call again server-side so the two
  checks can never drift apart. A confirmed result carries a `FileInputDialogResult` with the file name and its
  content already buffered into a `byte[]`; `Cancelled` is true if the user backed out.
- `dialogService.ShowDateInputDialogAsync(title, new DateInputDialogContent { Message = ..., InitialDate = ... })`
  opens `BaseDateInputDialog`, a single `FluentDatePicker<DateTime?>` with the same Confirm/Cancel shape; the
  result is a `DateInputDialogResult` with the picked `DateTime`. Typical use: "pick any day in the
  month whose imported data should be deleted" before a purge action, where the month is derived from
  the picked date by the caller.

All of them need Fluent's `IDialogService` and the `FluentProviders` that `BaseLayout` renders, exactly like
`IConfirmationService` above, and are plain Razor components with no CRUD dependency — usable from any page,
not only `BaseList`/`BaseCard` actions.

### Toasts

Toasts come from Fluent's `INotificationService`; the `FluentToastProvider` is part of the `FluentProviders` of `BaseLayout`.
`AddBlazorBaseFluentUI()` shows at most three at once at the top end of the window below the header:

```csharp
await Notifications.ShowSuccessToastAsync("Saved");
await Notifications.ShowErrorToastAsync("Saving failed", message: "The server did not answer.");
```

The calls return when the toast is queued, not when it closes.

---

## Routing

`QueryStringReader.TryReadValue(uri, name)` reads one query-string parameter out of an absolute or
relative URI. It takes no dependency on `Microsoft.AspNetCore.WebUtilities` and is pure, so it tests in
isolation. The first occurrence wins, the key match is case-insensitive, a fragment is ignored, and a
parameter that is present without a value yields an empty string rather than null — null means absent.

The CRUD `BaseList` resolves its deep-linked item with it; any page that answers to a query parameter
can do the same.
