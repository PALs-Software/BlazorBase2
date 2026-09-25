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

Every BlazorBase component stylesheet that paints a colour reads them — the shell, both navigation
components, the whole CRUD layer, the editors, the file components. Each declaration keeps the FluentUI
value it replaced as its fallback (`var(--color-bg-surface, var(--neutral-layer-1))`), so an application
that never links this file looks exactly as it did. Application code that uses the same names stays in
step automatically.

FluentUI's own controls — a `FluentButton`, a `FluentTextField`, dialog chrome — do not read CSS
custom properties at all. They paint from FluentUI's design system, which derives its ~170 tokens
algorithmically from two seed colours. `BaseLayout` feeds it both: it reads the resolved
`--color-accent` and `--color-neutral-base` after the first render and hands them to
`FluentDesignTheme` as `CustomColor` and `NeutralBaseColor`, so FluentUI regenerates its accent **and**
neutral ramps in the palette rather than in its default blue-grey.

Only hue and saturation carry over from the neutral seed — FluentUI interpolates white to black
through it and picks each layer by luminance, `Mode` deciding the direction. Keep the seed about as
saturated as the palette's own greys: the light end amplifies chroma, and a seed with ten points of
RGB spread turns the dialog background visibly mint.

That covers the ramp, not individual values. An application that has to pin one exact FluentUI token
can inject it — every one of them is a `DesignToken<T>` in
`Microsoft.FluentUI.AspNetCore.Components.DesignTokens` with `WithDefault(value)` for the global
default and `SetValueFor(element, value)` for a subtree. Reach for that sparingly: the hover and
active values are derived from the rest ones by deltas, so pinning a single swatch and leaving its
neighbours to the recipe is how a control ends up with a hover state that no longer matches it.

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

While the preference is `System`, a `matchMedia` listener watches the operating system and re-reads the
tokens when it flips. The stylesheet repaints on its own — what needs the listener is everything derived
from the tokens on the managed side: the accent handed to FluentUI and the `theme-color` meta tag, which
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
| `--color-accent` | Primary button, active tab, links, selection |
| `--color-text-on-accent` | Text on a filled accent surface |
| `--color-neutral-base` | Seed FluentUI derives its own neutral ramp from — see above |
| `--color-success` / `--color-warning` | Confirmations and warnings |
| `--color-danger` / `--color-danger-bg` / `--color-text-on-danger` | Delete and validation errors |
| `--space-1` … `--space-6` | Spacing scale, 4px to 32px |
| `--radius-sm` / `-md` / `-lg` / `-pill` | Corner radii |
| `--font-base` / `--font-display` / `--font-monospace` | Type stacks |

### Customising

Override the tokens in a stylesheet linked **after** the default one. Swapping the brand colour takes
three of them — the accent, its selection tint, and the neutral seed that takes FluentUI's own
controls along:

```css
:root {
    --color-accent: #6D28D9;
    --color-bg-selected: #6D28D914;
    --color-neutral-base: #878289;
}

:root[data-theme="dark"], :root:not([data-theme="light"]) {
    --color-accent: #A78BFA;
    --color-bg-selected: #A78BFA22;
    --color-neutral-base: #878289;
}
```

Everything else is neutral scaffolding. A palette whose light and dark sides sit in different hue
families — a warm surface in light, a cool one in dark — needs a seed per theme; one value covers both
when they share a family.

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
| `IConfirmationService` | Asks the user to confirm an action and reports whether they accepted. `FluentUiConfirmationService` is the default and needs FluentUI's `IDialogService`. |

`IConfirmationService` lives here because opening a dialog is a UI concern, not a CRUD one — an
application that never touches the data grid can still use it. The convenience registration currently
rides along with `AddBlazorBaseCrudComponents()` in `BlazorBase.CRUD`, which is deliberately separate
from `AddBlazorBaseComponents()`: a pure hosting backend that never renders interactively must not be
made to supply an `IDialogService`. Register it yourself with `TryAddScoped` if you want it without the
CRUD layer.

---

## Routing

`QueryStringReader.TryReadValue(uri, name)` reads one query-string parameter out of an absolute or
relative URI. It takes no dependency on `Microsoft.AspNetCore.WebUtilities` and is pure, so it tests in
isolation. The first occurrence wins, the key match is case-insensitive, a fragment is ignored, and a
parameter that is present without a value yields an empty string rather than null — null means absent.

The CRUD `BaseList` resolves its deep-linked item with it; any page that answers to a query parameter
can do the same.
