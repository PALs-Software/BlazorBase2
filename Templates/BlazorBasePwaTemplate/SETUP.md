# AppTemplate — finish the setup

Generated from the BlazorBase PWA template. Four steps and it runs.

## 1. Add the framework

The projects reference the BlazorBase2 framework at `Libs/`, which is deliberately not copied into
every new project. Add it as a git submodule:

```bash
git init
git submodule add BLAZORBASE_REPOSITORY_URL Libs
```

The URL above is whatever was passed as `--BlazorBaseUrl` when this project was created (the public
mirror by default). Point it at your own upstream if you develop against one.

If you would rather not use a submodule, any checkout of the framework works — either place it at
`Libs/`, or leave that folder empty and set the path yourself:

```bash
dotnet build -p:BlazorBaseLibsPath=../../BlazorBase2/
```

`BlazorBaseLibsPath` is declared in each project file and defaults to `Libs\`.

Package versions live in `Directory.Packages.props`, not in the project files — those reference
packages by name only. Add a package with one `PackageVersion` entry there plus a bare
`<PackageReference Include="…" />` in the project that needs it; a `Version` in a `.csproj` is an
error (NU1008).

## 2. Set the secrets

From `AppTemplate.Server`:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your connection string>"
```

`appsettings.json` ships a `Trusted_Connection` placeholder, so the connection string is only needed
if yours differs. There is no signing secret to set: access and refresh tokens are protected by the
ASP.NET Core Data Protection key ring, which the framework manages for you.

## 3. Create the database

```bash
dotnet ef database update --project AppTemplate.Server --startup-project AppTemplate.Server
```

## 4. Run it

```bash
dotnet run --project AppTemplate.Server
```

`DevelopmentAuthentication` is enabled in `appsettings.Development.json`, so the app signs you
straight in as `developer@apptemplate.local` with the `Admin` role — no login form while developing.
Change that email, or turn the section off, before anything leaves your machine.

Authentication uses the **cookie transport** (backend-for-frontend): the browser only holds an
`HttpOnly` session cookie, never a token, because the client is served from the same origin as the
API. `AuthCookies:SecureOnly` is `false` in `appsettings.Development.json` only, so the cookie also
works over plain HTTP locally. Every typed client that talks to the API needs
`.AddBlazorBaseCookieSessionHandlers()` (the `IAuthService` client takes `CsrfHeaderHandler` alone),
otherwise its writes are refused with 403. A native or service client that needs a bearer token still
works against the same server; see `Documentation/BlazorBase.User.Server.AuthTransports.md`.

## Where to go next

`Modules/Notes` (in `AppTemplate.Shared` and `AppTemplate.Client`) is the reference pattern for the
first real module. Copy its shape: the entity in `.Shared` carrying `[BaseCrud("route")]` and
`[DisplayKey]`, a `*Page.razor` hosting `<BaseList>`, a `*Card.razor` hosting `<BaseCard>`, and
co-located `.resx`/`.de.resx` files keyed by property name so captions need no markup.

The framework's own `Documentation/` folder is the authoritative reference for every library.
