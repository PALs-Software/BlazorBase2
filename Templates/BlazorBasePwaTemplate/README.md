# BlazorBase PWA Template

A `dotnet new` template for a Blazor WebAssembly + ASP.NET Core PWA wired up against
BlazorBase2 (Scenario 2 in [`../../Documentation/README.md`](../../Documentation/README.md)):
auth, CRUD, localization (English/German), theming, and PWA installability, plus a `Notes`
module demonstrating the generic `BaseList`/`BaseCard` pattern end to end.

## Install

From this folder (or point at a checked-out `BlazorBase2` clone elsewhere):

```bash
dotnet new install .
```

## Create a new project

```bash
dotnet new blazorbase-pwa -n MyApp -o MyApp
cd MyApp
```

This renames every `AppTemplate.*` project, folder and namespace to `MyApp.*`.

Pass `--BlazorBaseUrl` to choose the repository the generated project adds as its `Libs` submodule.
It defaults to the public mirror, which is the only one an outside consumer can reach:

```bash
dotnet new blazorbase-pwa -n MyApp -o MyApp --BlazorBaseUrl https://your-host/BlazorBase2.git
```

The generated project carries its own `SETUP.md` with the remaining four steps (submodule, secrets,
database, run), already naming your project rather than `AppTemplate`.

## How the template builds inside this repository

The generated project references the framework at `Libs\`, but this template lives inside the
framework itself, so there is nothing to put there. Each project file declares

```xml
<BlazorBaseLibsPath Condition="'$(BlazorBaseLibsPath)' == ''">..\Libs\</BlazorBaseLibsPath>
```

and [`Directory.Build.props`](Directory.Build.props) next to this file overrides it with the
repository root, so the template compiles and tests against the real sources. That file is excluded
from the template output, which is why a generated project falls back to `Libs\`.

Earlier revisions kept a real `Libs` here — first a symlink, which breaks Razor class library builds
([dotnet/razor#10574](https://github.com/dotnet/razor/issues/10574)), then a git submodule pointing at
this very repository, which cannot resolve in a squashed public mirror. Resolving the path through a
property needs neither.

## App shell defaults (keep them)

Apps generated from this template have lost these defaults more than once, because they look like
optional decoration and get dropped while an app is being reshaped. They are not optional. Every app
keeps all of them; a change to `AppTemplate.Client/Layout/MainLayout.razor(.cs)` must not remove one.

| Default | Where | What it must look like |
|---|---|---|
| Brand (app icon and app name) | `Layout/AppBrand.razor` in the `<Brand>` slot of `BaseLayout`, `BrandPosition="BrandPosition.Start"` | Icon from `wwwroot/icon.svg` plus the wordmark, as a link to the app root at the start of the top bar. Never fall back to the plain `AppTitle` text. |
| Command palette | `ShowCommandPalette="@IsAuthenticated"` and `NavigationItems="@NavigationItems"` on `BaseLayout` | The search field sits in the middle of the top bar for signed-in users (a search icon on a phone, Ctrl+K anywhere) and lists the navigation entries as pages. |
| Grouped sidebar with section headings | `BuildNavigationItems()` in `Layout/MainLayout.razor.cs` | A tree of groups (`Key`, `Label`, `Icon`, `ExpandedByDefault = true`, `Children`), not a flat list. The group labels are the section headings of the sidebar. |
| One navigation tree | `NavigationItems` passed to `BaseSideNavigation` and `BaseLayout.NavigationItems` | The rail, the mobile bottom bar (`IsMobilePrimary`) and the command palette read the same tree, so role visibility cannot diverge. |
| Theme toggle | `<BaseThemeToggle />` in `HeaderContent` | The theme switch in the top bar, next to the user menu. |
| User menu | `<BaseUserMenu />` (`BlazorBase.User.Components.UserMenu`) in `HeaderEndContent`, signed-in users only | The avatar button at the very end of the top bar opens the account menu: name and e-mail, language, theme, "Change password" (the framework page `/account/password`, switch off with `ShowChangePassword="false"`) and "Sign out". Never a plain avatar span that does nothing. Host entries such as "Settings" go into its `Items`. |

`AppTemplate.Tests/Layout/MainLayoutTests.cs` guards these defaults: it fails when the brand, the
palette, the group headings, the role gating of the admin group or the user menu disappear. If one of those tests
breaks, fix the layout, not the test. A test may only change together with an intentional redesign of
the shell.

What to change per app, and nothing else:

- **Icon.** Replace `wwwroot/icon.svg` (and the PWA icons in `wwwroot` if the app has them).
- **Wordmark.** Edit the app name in `Layout/AppBrand.razor`; keep the `<a class="app-brand">` structure.
  Its accessible name comes from `AppBrand.razor.resx` / `AppBrand.razor.de.resx`.
- **Groups and entries.** Replace the "Workspace" and "Administration" groups in `BuildNavigationItems()`
  with the app's own groups and entries (labels in `MainLayout.razor.resx` / `MainLayout.razor.de.resx`).
  Keep at least two groups with headings, mark the entries a phone needs most with `IsMobilePrimary`,
  and gate admin-only groups and their entries with `RequiredRole`.
- **Palette content.** Add providers (see below); the page entries need no work.
- **User menu entries.** When the app has a settings or profile page, pass it to `<BaseUserMenu Items="..." />`
  (`UserMenuItem` with `Key`, `Label`, `Href`, `Icon`); keep the language list in line with
  `UseBlazorBaseLanguageAsync` in `Program.cs` (`Languages` parameter, default `en`, `de`).

### New app checklist

1. `dotnet new blazorbase-pwa -n MyApp -o MyApp`, then follow `SETUP.md` in the generated project.
2. Swap `wwwroot/icon.svg` and the wordmark in `Layout/AppBrand.razor` for the app's identity.
3. Replace the template navigation groups in `Layout/MainLayout.razor.cs` with the app's groups; keep the
   group headings, `ExpandedByDefault`, `IsMobilePrimary` and the role gates.
4. Leave `ShowCommandPalette`, `NavigationItems`, `BrandPosition.Start`, `<BaseThemeToggle />` and
   `<BaseUserMenu />` in `Layout/MainLayout.razor` untouched (add `Items` to the menu if the app has a settings
   page), and keep `UseBlazorBaseLanguageAsync` in `Program.cs` so the menu's language choice takes effect.
5. Run the tests (`dotnet test`): `Layout/MainLayoutTests.cs` must pass. When you add or rename navigation
   groups, adapt the group-heading assertions there to the new labels instead of deleting them.

### Updating an app created from an older template

An app is a copy of the template at the time it was generated; later template changes do not reach it.
To catch up, compare these files of the app against the current template and port what is missing:

- `AppTemplate.Client/Layout/*` (`MainLayout.razor`, `MainLayout.razor.cs`, `MainLayout.razor.css`,
  `MainLayout.razor.resx`, `MainLayout.razor.de.resx`, `AppBrand.*`)
- `AppTemplate.Client/App.razor`
- `AppTemplate.Client/Program.cs` (and `AppTemplate.Client.csproj` for `BlazorWebAssemblyLoadAllGlobalizationData`)
- `AppTemplate.Client/wwwroot/index.html` (and `wwwroot/icon.svg`)
- `AppTemplate.Tests/Layout/MainLayoutTests.cs` (and the stubs it registers, `AppTemplate.Tests/Infrastructure/Stub*.cs`)

Then verify the defaults that were lost or missing before:

1. **Section headings.** The sidebar shows group headings: `BuildNavigationItems()` returns groups with `Children`, not a flat list.
2. **Command palette.** `BaseLayout` has `ShowCommandPalette="@IsAuthenticated"` and `NavigationItems="@NavigationItems"`; the search field appears in the middle of the top bar.
3. **Brand.** `<Brand><AppBrand /></Brand>` with `BrandPosition="BrandPosition.Start"` shows the icon and the app name, not just a title.
4. **User menu.** `HeaderEndContent` holds `<BaseUserMenu />` for signed-in users instead of a plain `<span class="avatar">`
   (remove that span, its `.avatar` CSS, the `UserInitials` code and the `AvatarTitle` resources); tapping the avatar
   opens the menu with "Change password" and "Sign out". `Program.cs` calls `UseBlazorBaseLanguageAsync(...)` after the development session.

Keep the app's own icon, wordmark, groups and entries while porting; only the structure is taken over.
Copy `Layout/MainLayoutTests.cs` along so the defaults stay guarded.

## The app shell: brand and command palette

`AppTemplate.Client/Layout/MainLayout.razor` hosts `BaseLayout` with these features switched on by default
(the table above is the contract, this section explains the two with their own files):

- **Brand.** `Layout/AppBrand.razor` renders the app logo (`wwwroot/icon.svg`) and the app name as a link to
  the app root at the start of the top bar, in the `Brand` slot of `BaseLayout` with
  `BrandPosition="BrandPosition.Start"`. It replaces the plain `AppTitle` text. Replace `wwwroot/icon.svg` (and
  the wordmark in `AppBrand.razor`) to give the app its own identity; its accessible name comes from
  `AppBrand.razor.resx` / `AppBrand.razor.de.resx`.
- **Command palette.** `ShowCommandPalette="@IsAuthenticated"` shows the search field to signed-in users in the center of the header (a
  search icon on a phone) and opens the palette with Ctrl+K from anywhere. It lists the entries of
  `NavigationItems` as pages. The palette services are registered by `AddBlazorBaseUserClient()` (through
  `AddBlazorBaseComponents()`), so no extra registration is needed in `Program.cs`.

To add more entries, register providers in `AppTemplate.Client/Program.cs`:

```csharp
builder.Services.AddCommandPaletteProvider<NoteSearchProvider>();
builder.Services.AddCommandPaletteProvider([new CommandPaletteEntry { Id = "help", Title = "Help", Group = "Pages", Href = "help" }]);
```

`NoteSearchProvider` is a class implementing `ICommandPaletteProvider`. See "Command palette" in
[`../../Documentation/BlazorBase.Components.md`](../../Documentation/BlazorBase.Components.md) for the provider
contract, permission rules and ranking. To remove the palette or the brand again, drop `ShowCommandPalette` or
the `<Brand>` block from `MainLayout.razor`.

## Where to go next

- `Modules/Notes` (in `.Shared`/`.Client`) is the reference pattern for adding a first real
  module — copy its shape: entity in `.Shared` with `[BaseCrud("route")]`, a `*Page.razor`
  hosting `<BaseList>`, a `*Card.razor` hosting `<BaseCard>`, co-located `.resx`/`.de.resx`.
- `../../Documentation/` is the authoritative reference for every BlazorBase2 library.
