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

**Header order.** From left to right the end of the header shows `HeaderContent`, the help button
(`ShowHelp`), the theme toggle (`ShowThemeToggle`), the settings button (`ShowSettings`) and finally
`HeaderEndContent`.

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
        <FluentAnchor Href="/account" Class="base-header-wide-only">Account</FluentAnchor>
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
  (`#base-main`) and inside dialogs (`fluent-dialog`) — never inside the help panel, the header or the navigation.
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

### File and date input dialogs

Two more FluentUI dialogs live next to `IConfirmationService`, for the two inputs a destructive or
import-style page action tends to need and that FluentUI has no ready-made dialog for: picking one file
and picking one date.

- `dialogService.ShowFileInputDialogAsync(title, new FileInputDialogContent { Message = ..., FileInputCaption = ..., FileFilter = ".xlsx", MaxFileSize = 20 * 1024 * 1024 })`
  opens `BaseFileInputDialog`, a FluentUI `FluentInputFile` (stream mode) wrapped in Confirm/Cancel
  buttons. The confirm button stays disabled until a file both matches `FileFilter` (comma separated
  extensions, e.g. `.csv,.txt`) and is at or under `MaxFileSize` (0 or negative disables the size
  check); the validation itself lives in the dependency-free `FileInputValidator.Validate(...)`, reused
  here and safe to call again server-side so the two checks can never drift apart. The dialog result
  (`dialog.Result` on the returned `IDialogReference`) carries a `FileInputDialogResult` with the file
  name and its content already buffered into a `byte[]`, or `Cancelled: true` if the user backed out.
- `dialogService.ShowDateInputDialogAsync(title, new DateInputDialogContent { Message = ..., InitialDate = ... })`
  opens `BaseDateInputDialog`, a single `FluentDatePicker` with the same Confirm/Cancel shape; the
  result is a `DateInputDialogResult` with the picked `DateTime`. Typical use: "pick any day in the
  month whose imported data should be deleted" before a purge action, where the month is derived from
  the picked date by the caller.

Both dialogs need FluentUI's `IDialogService`, exactly like `IConfirmationService` above, and are plain
Razor components with no CRUD dependency — usable from any page, not only `BaseList`/`BaseCard` actions.
Escape closes either of them.

### Confirmation dialog and dialog behavior

`dialogService.ShowConfirmationDialogAsync(title, new ConfirmationDialogContent { Message = ..., ConfirmButtonText = ..., CancelButtonText = ..., IsDestructive = true })`
opens `BaseConfirmationDialog`; `IConfirmationService` uses it. A destructive confirmation shows a warning
icon and a danger-styled confirm button, and starts on **Cancel** so a stray Enter cannot confirm it. Escape
closes it, also when it is open on top of another dialog (the discard prompt over a card).

`DialogBehavior` is the wrapper that gives a dialog the keyboard behavior a modal owes: focus moves into it
when it opens (to the element marked `data-autofocus`, otherwise the first field), everything outside it is
made `inert` so Tab cannot leave it and the page behind cannot be clicked, `CommitPendingEditOnEscape`
commits a half-typed field before Escape is handled (so an unsaved-changes check sees it), and focus is handed
back to the opener when the dialog closes and left on nothing. `DismissOnEscape` closes the dialog on
Escape. FluentUI's own focus trap is not used for dialogs that can host another dialog: two traps fight over
focus.

---

## Routing

`QueryStringReader.TryReadValue(uri, name)` reads one query-string parameter out of an absolute or
relative URI. It takes no dependency on `Microsoft.AspNetCore.WebUtilities` and is pure, so it tests in
isolation. The first occurrence wins, the key match is case-insensitive, a fragment is ignored, and a
parameter that is present without a value yields an empty string rather than null — null means absent.

The CRUD `BaseList` resolves its deep-linked item with it; any page that answers to a query parameter
can do the same.
