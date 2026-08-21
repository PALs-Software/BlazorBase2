# BlazorBase

Reusable Blazor base libraries (CRUD, User/Identity, Charting, Mailing) shared across
applications. Open [BlazorBase.slnx](BlazorBase.slnx) in Visual Studio to build all projects.

## Projects

| Project | Purpose |
|---|---|
| `BlazorBase.Localization` | Localizer composition, usable without Blazor |
| `BlazorBase.Components` | Layout, navigation, theming, standalone editors and routing helpers |
| `BlazorBase.CRUD` | Generic CRUD components, models and services |
| `BlazorBase.CRUD.Generators` | Source generators for the CRUD layer |
| `BlazorBase.CRUD.Benchmarks` | Benchmarks for the CRUD layer |
| `BlazorBase.Chart` | Charting components |
| `BlazorBase.Mailing` | Mailing/notification infrastructure |
| `BlazorBase.User` | Shared user/identity components and models |
| `BlazorBase.User.Server` | Server-side auth controllers and services |
| `BlazorBase.User.Wasm` | WebAssembly client auth integration |
| `BlazorBase.User.Maui` | MAUI client auth integration |

## Documentation

Per-library documentation lives under [Documentation/](Documentation/), starting with
[Documentation/README.md](Documentation/README.md).

## Highlights

- **`BlazorBase.CRUD`** — generic list/card CRUD components with an abstracted `IBaseDataProvider<T>` seam (EF Core server / REST client). The fluent query builder decomposes `Where` lambdas into `FilterDescriptor` trees supporting `==`, `!=`, `>`, `<`, `Contains`, `StartsWith`, `EndsWith`, `IsNull`, and `In` (set-membership, translates to SQL `IN (…)`). See [Documentation/BlazorBase.CRUD.md](Documentation/BlazorBase.CRUD.md) for the full operator catalog.

## Consumption

This repository is intended to be consumed as a **git submodule**. Reference the contained
`.csproj` files directly via `<ProjectReference>` from the host solution.

## License

MIT — see [LICENSE.md](LICENSE.md).
