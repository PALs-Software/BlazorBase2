# BlazorBase.Components

Razor Class Library (net10.0). The shared UI layer: application shell, navigation, theming, and the
standalone editors. It references neither `BlazorBase.CRUD` nor `BlazorBase.User` — both of those sit
above it — so an application that only needs a data grid does not have to pull in the auth stack to get
a layout, and a rich-text editor is not a CRUD concept.

---

## Themes

Two default themes ship as one stylesheet of CSS custom properties. Link it once:

```html
<link rel="stylesheet" href="_content/BlazorBase.Components/css/blazorbase.css" />
```

The shell reads these tokens: `BaseLayout`, both navigation components and the CRUD `BaseList`.
Everything else still styles itself from FluentUI's own design tokens, so linking this file themes the
frame around the content, not yet every control inside it — migrating a stylesheet means replacing a
`var(--neutral-layer-1)` with `var(--color-bg-surface, var(--neutral-layer-1))`, which keeps the
FluentUI value as the fallback. Application code that uses these names stays in step automatically.

### Applying and switching

`IThemeService` owns the preference. It is deliberately split so that nothing touches the document
before the first render:

| Member | Purpose |
|---|---|
| `InitializeAsync()` | Reads the preference. No DOM access, safe from `OnInitializedAsync`. |
| `ApplyAsync()` | Writes it onto `<html>`. Uses JS interop — call it after the first render. |
| `SetThemeAsync(preference)` | Sets, applies, persists, and raises `ThemeChanged`. |
| `CurrentTheme` | `System`, `Light` or `Dark`. |

`BaseLayout` already does both calls. Hosting it is all an application needs; `BaseThemeToggle` gives
the user a control that cycles system → light → dark. Register the services with
`services.AddBlazorBaseComponents()` — `AddBlazorBaseUserClient()` already calls it, so an application
on the user stack needs nothing extra.

Where the preference comes from: the signed-in user's profile claim seeds a browser that has no choice
of its own, so the setting follows the user to a new device. An explicit choice made here wins from
then on, which is also what makes it survive a reload. Only `SetThemeAsync` writes to local storage —
persisting the choice to the profile is the host's call, which `UserPreferencesPanel` in
`BlazorBase.User` does.

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
| `--color-bg-hover` | Hover on rows and menu entries |
| `--color-bg-selected` | Selected row, active mobile tab |
| `--color-border` | Borders, dividers, grid lines |
| `--color-border-strong` | Emphasised edges, the sheet handle |
| `--color-border-focus` | Keyboard focus ring |
| `--color-text-primary` | Body text, headings, cell values |
| `--color-text-secondary` | Secondary labels, column headers, icons |
| `--color-text-muted` | Placeholders, disabled state, empty state |
| `--color-accent` | Primary button, active tab, links, selection |
| `--color-text-on-accent` | Text on a filled accent surface |
| `--color-success` / `--color-warning` | Confirmations and warnings |
| `--color-danger` / `--color-danger-bg` / `--color-text-on-danger` | Delete and validation errors |
| `--space-1` … `--space-6` | Spacing scale, 4px to 32px |
| `--radius-sm` / `-md` / `-lg` / `-pill` | Corner radii |
| `--font-base` / `--font-display` / `--font-monospace` | Type stacks |

### Customising

Override the tokens in a stylesheet linked **after** the default one. Swapping the brand colour takes
two of them; everything else is neutral scaffolding:

```css
:root {
    --color-accent: #6D28D9;
    --color-bg-selected: #6D28D914;
}

:root[data-theme="dark"], :root:not([data-theme="light"]) {
    --color-accent: #A78BFA;
    --color-bg-selected: #A78BFA22;
}
```

---

## Layout and navigation

`BaseLayout` renders the header, a navigation slot and the body, and wires the theme and language
services. One `NavigationItem` tree drives both presentations, so desktop and mobile cannot drift
apart:

- `BaseSideNavigation` — the desktop rail.
- `BaseBottomNavigation` — the mobile tab bar. Entries flagged `IsMobilePrimary` become tabs; the rest
  land behind a "More" bottom sheet that also handles groups (with a back step) and actions such as
  logging out.

`NavigationVisibility` filters the tree once against the current principal, dropping entries whose
`RequiredRole` does not match and groups left with no visible children.

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

---

## Editors and viewers

| Component | Purpose |
|---|---|
| `RichTextEditor` | Content-editable HTML editor. Sanitises through the host's `IHtmlSanitizer` when one is registered. |
| `SanitizedHtml` | Renders untrusted HTML through the same seam. |
| `DiffViewer` | Unified or side-by-side file diff with syntax highlighting and per-line comment anchors. |
| `FileTree` | Collapsible file tree over `FileTreeNode`. |

`IHtmlSanitizer` is a seam, not an implementation, and the two components fall back differently when
no sanitizer is registered. `SanitizedHtml` HTML-encodes the value, so it stays safe and simply shows
the markup as text. `RichTextEditor` passes the value through unchanged, which is correct for content
an author already owns and a real risk for anything else — register a sanitizer before letting one
user's markup reach another user.

---

## Services

| Service | Purpose |
|---|---|
| `IThemeService` | Light/dark/system preference, see above. |
| `ILanguageService` | Current UI language and switching. |
| `IFormFactor` | Reports the host platform and form factor. The seam only; the WebAssembly and MAUI packages each register their own implementation. |
