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

## Where to go next

- `Modules/Notes` (in `.Shared`/`.Client`) is the reference pattern for adding a first real
  module — copy its shape: entity in `.Shared` with `[BaseCrud("route")]`, a `*Page.razor`
  hosting `<BaseList>`, a `*Card.razor` hosting `<BaseCard>`, co-located `.resx`/`.de.resx`.
- `../../Documentation/` is the authoritative reference for every BlazorBase2 library.
