# BlazorBase.User.Server

The server-side companion to [`BlazorBase.User`](BlazorBase.User.md). Bundles ASP.NET Core Identity, JWT issuance/validation, refresh-token persistence, an abstract base for the `/api/auth` and `/api/user` controllers, and a `IBaseDataProvider<UserModel>` that integrates user management into the BlazorBase.CRUD pipeline.

> Target framework: `net10.0` · References `BlazorBase.User` and `BlazorBase.CRUD`.

---

## Table of Contents

- [Architecture & Overview](#architecture--overview)
- [Getting Started](#getting-started)
- [Entities & DbContext](#entities--dbcontext)
- [Access tokens (machine-to-machine)](#access-tokens-machine-to-machine)
- [Services](#services)
- [Controllers](#controllers)
- [Configuration: JwtSettings](#configuration-jwtsettings)
- [Upgrade / Breaking Behavior](#upgrade--breaking-behavior)
- [REST API Reference](#rest-api-reference)
- [Code Examples](#code-examples)

---

## Architecture & Overview

```
┌──────────────────────────────────────────────────────────┐
│  HTTP                                                    │
│  /api/auth/{status,setup,login,refresh,logout}           │
│  /api/user/me, /api/user/me/settings                     │
│  /api/base/users  (BlazorBase.CRUD endpoints)            │
├──────────────────────────────────────────────────────────┤
│  Controllers                                             │
│   AuthControllerBase<TUser>                              │
│   UserControllerBase<TUser>                              │
│  ── consumer derives concrete controllers (TUser=AppUser)│
├──────────────────────────────────────────────────────────┤
│  Services                                                │
│   TokenService<TUser>      JWT generation & validation   │
│   UserDataProvider<TUser>  IBaseDataProvider<UserModel>  │
├──────────────────────────────────────────────────────────┤
│  Data                                                    │
│   BaseUser : IdentityUser  (DisplayName, ThemePreference,│
│                              Language, IsActive,         │
│                              CreatedAt)                  │
│   RefreshToken     (Token = SHA-256 hash, UNIQUE index,  │
│                      UserId index)                       │
│   AccessToken      (TokenHash = SHA-512, Prefix index,   │
│                      optional UserId — see below)        │
│   BaseUserDbContext<TUser> : IdentityDbContext<TUser>    │
├──────────────────────────────────────────────────────────┤
│  Identity + JWT bearer auth                              │
│   PasswordOptions  (length≥8, digit, lower, upper)       │
│   JwtBearer        (configured from "JwtSettings")       │
│   BlazorBaseAccessToken  (opt-in, additive scheme)       │
└──────────────────────────────────────────────────────────┘
```

`TUser` is the consumer's concrete user class (e.g. `AppUser`) that inherits from `BaseUser`. `TContext` is the consumer's `DbContext` deriving from `BaseUserDbContext<TUser>`. This generic approach lets the host extend the user with custom properties while keeping the auth pipeline intact.

---

## Getting Started

### 1. Project reference

```xml
<ProjectReference Include="..\Libs\BlazorBase.User.Server\BlazorBase.User.Server.csproj" />
```

### 2. Concrete user + DbContext

```csharp
// OmniLog.Web/Data/Entities/AppUser.cs
public class AppUser : BaseUser
{
    public string? AvatarUrl { get; set; }
}

// OmniLog.Web/Data/AppDbContext.cs
public class AppDbContext(DbContextOptions<AppDbContext> options) : BaseUserDbContext<AppUser>(options)
{
    public DbSet<Product>  Products  => Set<Product>();
    public DbSet<Activity> Activities => Set<Activity>();
}
```

### 3. Register services

```csharp
// Program.cs
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));

builder.Services.AddBlazorBaseUserServer<AppUser, AppDbContext>(builder.Configuration);

// Map admin user CRUD endpoints
app.MapBlazorBaseUserAdminEndpoints();      // → /api/base/users  (Admin role)
```

`AddBlazorBaseUserServer<TUser, TContext>` registers:
- `IdentityCore<TUser>` with `AddRoles<IdentityRole>().AddEntityFrameworkStores<TContext>().AddDefaultTokenProviders()`
- `Authentication` → `JwtBearer` configured from `IConfiguration["JwtSettings:*"]`
- `Authorization`
- `TokenService<TUser>` (scoped) — receives all `IClaimsAugmentor<TUser>` instances via `IEnumerable<>`
- `BaseUserDbContext<TUser>` (scoped) — bridged to the host's concrete `TContext` via `serviceProvider.GetRequiredService<TContext>()`
- `IBaseDataProvider<UserModel>` → `UserDataProvider<TUser>`
- `IHttpContextAccessor`
- A **rate limiter** with a fixed-window policy named `"BlazorBaseAuth"` (partitioned by client IP) applied to the `login`/`refresh`/`setup` actions. Thresholds come from `JwtSettings:RateLimit:*` (see [Configuration](#configuration-jwtsettings)). The policy is only enforced once the host adds `app.UseRateLimiter()` to its middleware pipeline — registering the policy without the middleware is harmless.

> **Host requirement:** because `BaseUserDbContext<TUser>` is bridged from `TContext` through the container, the host **must** register `TContext` itself as a scoped service — i.e. via `AddDbContext<TContext>(...)` as shown above. Registering `TContext` with any other lifetime (or not at all) breaks the bridge and `AuthControllerBase`/`UserDataProvider` resolution.

#### DI helper

To add extra claims to every issued token register an `IClaimsAugmentor<TUser>` implementation:

```csharp
// Program.cs — call before AddBlazorBaseUserServer or after, order does not matter
builder.Services.AddClaimsAugmentor<AppUser, AppClaimsAugmentor>();
```

`AddClaimsAugmentor<TUser, TAugmentor>` registers `TAugmentor` as a scoped `IClaimsAugmentor<TUser>`. Calling it multiple times registers multiple augmentors; they run in registration order.

### 4. Derive controllers

`AuthControllerBase` and `UserControllerBase` are `abstract`. Provide thin concrete classes so ASP.NET Core picks them up:

```csharp
[ApiController]
public class AuthController(
    UserManager<AppUser> users,
    RoleManager<IdentityRole> roles,
    TokenService<AppUser> tokens,
    AppDbContext db,
    IConfiguration config) : AuthControllerBase<AppUser>(users, roles, tokens, db, config);

[ApiController]
public class UserController(UserManager<AppUser> users)
    : UserControllerBase<AppUser>(users);
```

### 5. Add JwtSettings to appsettings.json

```json
{
  "JwtSettings": {
    "Secret": "<at-least-32-bytes-random>",
    "Issuer": "OmniLog",
    "Audience": "OmniLogClients",
    "ExpirationMinutes": 60,
    "RefreshExpirationDays": 30
  }
}
```

`Secret` is required and must be **at least 32 bytes** (256 bits, the HMAC-SHA256 key size); the registration throws `InvalidOperationException` if it is missing or shorter (**USRV-05**).

> **Enforce the rate limiter:** to apply the `"BlazorBaseAuth"` policy registered by `AddBlazorBaseUserServer`, add `app.UseRateLimiter();` **after `app.UseRouting()` and before the endpoint mapping** — the policy is attribute-based (`[EnableRateLimiting]`), so routing must have selected the endpoint before the limiter runs; placing it before `UseRouting` silently applies nothing. `"BlazorBaseAuth"` is a **reserved policy name** — a host may still call `AddRateLimiter` for its own *differently* named policies. The partition key is the client IP (IPv6 is masked to its /64 prefix so an attacker cannot get a fresh bucket per address); the request body is not available at partition time, so per-email keying is intentionally not used. Hosts behind a reverse proxy/CDN **must** configure `ForwardedHeaders`/`KnownProxies` before `UseRateLimiter` so the real client IP is seen — otherwise all clients collapse into one bucket (a self-inflicted `429` for everyone) and per-client isolation is lost. Tune `JwtSettings:RateLimit:*` for your topology.

### 6. Apply migrations

Identity tables + `RefreshTokens` come from `BaseUserDbContext`:

```bash
dotnet ef migrations add InitialUser
dotnet ef database update
```

> **Migration required on upgrade (USRV-04):** the `RefreshToken.FamilyId` column and its index are new. Add a migration against your concrete `DbContext` and **apply it to the database before the new application version starts serving traffic** — this is an additive, migrate-then-deploy change. While the new code runs against the old schema, every `/api/auth` call throws `500` (the column is referenced on every login/refresh/setup), so migrate first, then deploy.
> ```bash
> dotnet ef migrations add AddRefreshTokenFamilyId
> dotnet ef database update
> ```
> Pre-existing rows default `FamilyId` to `Guid.Empty`. The framework treats an empty family as a **singleton** — a replayed legacy token revokes only itself, and the family sweep is additionally scoped to the token's own `UserId` — so no cross-user forced-logout can occur during the transition. To adopt real per-session families immediately, backfill a unique value per existing row in the migration (e.g. `UPDATE "RefreshTokens" SET "FamilyId" = "Id" WHERE "FamilyId" = '00000000-0000-0000-0000-000000000000';`); otherwise families form naturally as users log in again.

---

## Entities & DbContext

### `BaseUser : IdentityUser`

```csharp
[MaxLength(100)] public string DisplayName { get; set; } = "";
[MaxLength(20)]  public string ThemePreference { get; set; } = "System";
[MaxLength(10)]  public string Language { get; set; } = "de";
public bool      IsActive { get; set; } = true;
public DateTime  CreatedAt { get; set; } = DateTime.UtcNow;
```

### `RefreshToken`

```csharp
[Index(nameof(Token), IsUnique = true)]
[Index(nameof(UserId))]
[Index(nameof(FamilyId))]
public class RefreshToken
{
    public Guid     Id        { get; set; }
    [Required, MaxLength(500)]
    public string   Token     { get; set; } = "";
    public string   UserId    { get; set; } = "";
    public Guid     FamilyId  { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool     IsRevoked { get; set; }
}
```

`Token` stores the **SHA-256 hex hash** of the refresh token (`TokenService<TUser>.HashRefreshToken`), not the raw value — a 64-character hex string, well within `MaxLength(500)` and the unique index. The raw token is only ever handed to the client once, in the `LoginResponse` returned by `setup`/`login`/`refresh`; the server hashes an incoming token before every DB lookup (`refresh`, `logout`) and never stores or logs the raw value. This means a leaked database dump does not expose usable refresh tokens.

`FamilyId` groups a login and all refresh tokens rotated from it into one *token family*. A fresh `login`/`setup` starts a new family; each rotation on `refresh` inherits the presenting token's `FamilyId`. If an **already-rotated (revoked) token is replayed** — the classic signature of a stolen-and-then-legitimately-rotated token — `refresh` revokes the **entire family**, cutting off the thief and the victim alike and forcing a clean re-login (theft detection, **USRV-04**).

### `BaseUserDbContext<TUser> : IdentityDbContext<TUser>`

```csharp
public abstract class BaseUserDbContext<TUser>(DbContextOptions options) : IdentityDbContext<TUser>(options)
    where TUser : BaseUser
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AccessToken> AccessTokens => Set<AccessToken>();
}
```

---

## Access tokens (machine-to-machine)

A refresh token belongs to a browser session; an **access token** is the credential a *program* carries — an MCP server, a CI job, a script. It is long-lived, hashed at rest, individually revocable, and authenticated by its own scheme so it never competes with the JWT a browser session uses.

### Registration

```csharp
builder.Services.AddBlazorBaseAccessTokens<AppDbContext>(builder.Configuration);
```

Registers the scheme **additively** — the app's default (JWT) scheme is untouched. Endpoints opt in one at a time:

```csharp
app.MapMcp("/api/mcp")
   .RequireAuthorization(policy => policy
       .AddAuthenticationSchemes(AccessTokenAuthenticationDefaults.AuthenticationScheme)
       .RequireAuthenticatedUser());
```

### `IAccessTokenService`

| Method | Purpose |
|---|---|
| `CreateAsync(name, expiresAt, userId?)` | Mints a token. **The plain-text secret is returned here and never again** — only its SHA-512 hash is stored. |
| `ValidateAsync(secret)` | Prefix lookup → constant-time hash match → fresh database check of `IsRevoked`/`ExpiresAt`. |
| `RevokeAsync(tokenId, userId?)` | Revokes. Pass `userId` to refuse another account's token. |
| `ListAsync(userId?)` | Non-secret summaries; never carries the hash. |
| `TouchAsync(tokenId)` | Updates `LastUsedAt`, throttled to once a minute per token. |

`userId` is optional throughout because both shapes of application have to fit: one where every token belongs to the account that minted it, and one where the data is a single shared collection with no per-account ownership. **An application of the first kind must pass the current user's id to `ListAsync` and `RevokeAsync`** — omitting it there is what silently turns a per-user token list into an everyone's-tokens list.

### Why validation always re-reads the database

The prefix cache answers only *which rows share this prefix, and what is their hash* — facts that never change for an existing token. Whether a token is still valid **right now** is re-read from the database on every successful hash match, and a cache miss falls through to the database rather than being treated as "unknown token".

Both halves matter as soon as more than one instance runs:

| | Cache-authoritative | This implementation |
|---|---|---|
| Token minted on instance A | Instance B returns **401 until it restarts** | works immediately |
| Token revoked on instance A | Instance B **keeps accepting it** | rejected on the next request |

The cost is one indexed primary-key lookup per authenticated request. The alternative is a window — bounded only by process lifetime — in which a revoked credential still works. `AccessTokenCrossInstanceTests` pins all four cases; they are the tests a preload-once implementation fails while passing every single-instance test.

A cache *miss* never inserts anything, so probing with random secrets — each producing a distinct, non-existent prefix — cannot grow the dictionary. Without that property the cache would be an unbounded memory sink addressable by anyone who can reach the endpoint.

### Configuration

```json
"AccessTokenSettings": {
  "SecretPrefix": "myapp_",
  "SecretByteLength": 32,
  "BruteForceBaseDelayMs": 200,
  "BruteForceMaxDelayMs": 10000
}
```

`SecretPrefix` makes a leaked string recognizable as your application's token, to a human and to a secret scanner. **Treat it as set-once:** the stored `Prefix` is a fixed-length slice taken by position, so changing it invalidates every token already issued. A prefix too long to fit the column fails at **startup** (`AccessTokenSettingsValidator`), not at the first mint.

`SecretByteLength` below 32 is raised to 32 rather than honoured — a shorter secret is never what the caller actually wants, and quietly accepting it would weaken every token.

Failed attempts incur a per-client exponential back-off keyed on the **transport-level remote address**, never on `X-Forwarded-For`: a caller able to choose its own bucket could reset the delay at will. Behind a reverse proxy, configure `UseForwardedHeaders` with a trusted-proxy list so that address is the real client. The state is per-process, so several instances give an attacker one ladder each — a brake on guessing, not a global rate limit.

### What is deliberately *not* here

**Scopes.** Authentication is generic; authorization is not. An application that needs "this token may read repository X but not write project Y" builds that on top of the returned `ValidatedAccessToken`, in its own domain vocabulary. Pushing a scope model into the framework would either be too narrow for the next application or so abstract it stops carrying meaning.

**A management UI or REST controller.** Minting and revoking are service calls; how they are exposed — an admin page, a controller, a CLI switch — is the host's decision.

### Migration required on upgrade

`AccessTokens` is a new table on `BaseUserDbContext`. Every deriving application needs a migration, even one that never calls `AddBlazorBaseAccessTokens`:

```bash
dotnet ef migrations add AddAccessTokens
```

---

## Services

### `IClaimsAugmentor<TUser>`

A DI seam for injecting extra claims into every issued access token. Implement this interface to emit claims (e.g. `household_id`) without subclassing `TokenService`:

```csharp
public interface IClaimsAugmentor<TUser> where TUser : BaseUser
{
    Task<IEnumerable<Claim>> GetAdditionalClaimsAsync(TUser user, IList<string> roles);
}
```

Register implementations with `AddClaimsAugmentor<TUser,TAugmentor>(services)` (see [DI helper](#di-helper)). Multiple augmentors are applied in registration order. When none are registered the token's claim set is identical to the pre-augmentor behavior.

### `TokenService<TUser>`

| Method | Signature | Purpose |
|---|---|---|
| `GenerateAccessToken` | `async Task<string>` | Creates a signed JWT with built-in claims (`NameIdentifier`, `Email`, `displayName`, `themePreference`, `language`, `Role` per role) then appends claims from each registered `IClaimsAugmentor<TUser>`. Lifetime from `JwtSettings:ExpirationMinutes` (default 15). |
| `GenerateRefreshToken` | `string` | 64-byte base64 random token via `RandomNumberGenerator`. This is the **raw** token, returned to the client. |
| `HashRefreshToken` | `string` | Returns the SHA-256 hex hash of the given token. Used to compute the value persisted in `RefreshToken.Token` and to hash an incoming token before every DB lookup — the raw token is never stored. |
| `ValidateAccessToken` | `ClaimsPrincipal?` | Returns a `ClaimsPrincipal?` when the token's signature, issuer, audience **and lifetime** all validate; otherwise `null`. (Lifetime is validated as of **USRV-08** — the method no longer accepts expired tokens.) |

Constructor: `TokenService<TUser>(IConfiguration configuration, IEnumerable<IClaimsAugmentor<TUser>> claimsAugmentors)`. The `IEnumerable` is empty when no augmentors are registered — existing hosts keep working.

### `UserDataProvider<TUser>`

Implements `IBaseDataProvider<UserModel>`. Hosts the `BlazorBase.CRUD` glue for user management:
- **`GetListAsync`** — applies filter ops on `DisplayName`, `Email`, `IsActive`; supports sort on `DisplayName`, `Email`, `CreatedAt`, `IsActive`. Uses `EF.Functions.Like` for `Contains` filters.
- **`GetByIdAsync`** — `UserManager.FindByIdAsync`.
- **`CreateAsync`** — `UserManager.CreateAsync` + `AddToRoleAsync`. **Checks the password and the role
  before writing anything**, raising `BaseValidationException` (a localized `400`) for a missing
  password, a missing role, or a role that does not exist in `AspNetRoles`. Identity's own errors
  (password policy, duplicate email) are re-raised the same way instead of as a `500`.

  > **The roles have to exist.** The names offered to an administrator come from the app's
  > `IUserRoleProvider`, while the rows are seeded by `AuthControllerBase` while creating the first
  > administrator. A database that never went through that path — one bootstrapped by
  > `DevelopmentAuthentication`, for instance — has only the roles the development account itself
  > uses, and assigning any other one fails. Seed the app's full role set at startup rather than
  > relying on the setup flow.
- **`PatchAsync`** — switches over allowed keys: `DisplayName`, `Email`, `IsActive`, `Role` (re-assigns role), `Password` (resets via `GeneratePasswordResetTokenAsync`, then **revokes all of that user's active refresh tokens** so existing sessions cannot continue using the old password's grant).
- **`DeleteAsync`** — refuses to delete the currently authenticated user. After a successful delete it invokes every registered `IUserDeletionHandler` (see below) so hosts can clean up per-user external resources.

The `IHttpContextAccessor` dependency is what makes the self-delete check work.

#### `IUserDeletionHandler` (user-deletion seam)

An optional extension point in `Lifecycle/IUserDeletionHandler.cs`: `Task OnUserDeletedAsync(string userId, CancellationToken)`. `UserDataProvider<TUser>` takes an injected `IEnumerable<IUserDeletionHandler>` (empty by default — behavior unchanged for hosts that register none) and calls each handler **once, after** the Identity row is successfully deleted. Handlers are best-effort and must not throw for recoverable failures. Register one per external resource, e.g. `services.AddScoped<IUserDeletionHandler, MyExternalTokenCleanupHandler>()`.

---

## Controllers

### `AuthControllerBase<TUser>` *(abstract)*

Route: `[Route("api/auth")]`

| Method | Route | Auth | Body | Returns |
|---|---|---|---|---|
| `GET` | `status` | anon | — | `AuthStatusResponse { HasUsers, DevelopmentAuthenticationEnabled }` |
| `POST` | `setup` | anon | `SetupRequest` | `LoginResponse` (creates first user, assigns Admin) |
| `POST` | `login` | anon | `LoginRequest` | `LoginResponse` |
| `POST` | `refresh` | anon | `RefreshTokenRequest` | `LoginResponse` (atomically revokes the presented token, issues a new one) |
| `POST` | `logout` | anon | `RefreshTokenRequest` | `NoContent` (revokes the presented refresh token; the token itself is the credential) |
| `POST` | `development-login` | anon | — | `LoginResponse` for the configured development account — **`404` unless the Development environment has it switched on** |

Behavior details:
- `setup` returns `409 Conflict` if any user already exists.
- `login` checks `IsActive`; deactivated users get `401 Unauthorized`. **Anti-enumeration (USRV-07):** an unknown email, a wrong password and a locked-out account all return the *same* generic `401` body (`"Invalid email or password."`), and the unknown-email path performs equivalent password-hashing work so response timing does not reveal whether an account exists. The lockout counter is still enforced; it just no longer surfaces a distinct "locked" message. (Consequence: a locked-out user is not told they are locked out — a host that wants that UX must surface it another way.)
- `refresh` looks up the token by its **SHA-256 hash** (`TokenService.HashRefreshToken`) and rejects it with `401 Unauthorized` when unknown, expired, or replayed. It also rejects a refresh for a **locked-out** user (**USRV-10**). Rotation is **single-use and atomic**: the presented token is revoked with a conditional update (`WHERE Id = … AND !IsRevoked`) inside the same database transaction that issues the new token pair; if the conditional update affects zero rows — because the token was already revoked, e.g. by a concurrent/replayed refresh — the whole request fails with `401 Unauthorized` instead of silently issuing a second token pair. **Theft detection (USRV-04):** replaying an *already-rotated* token revokes the whole token family (all tokens sharing its `FamilyId`), so a stolen token that has since been rotated cannot be used and the legitimate session is also forced to re-login.
- `logout` is `[AllowAnonymous]`: presenting a valid refresh token is itself the authority to revoke it, so logout succeeds even when the caller's access token has already expired. Only the single presented token (by hash) is revoked; other sessions/tokens for the same user are untouched. Logging out with an unknown/already-revoked token still returns `204 No Content` (idempotent).
- `EnsureRolesExist` creates all roles in `RolesToSeed` on first setup.

**Residual limitations:** access tokens are stateless JWTs and remain valid until their `ExpirationMinutes` lifetime lapses, even after a logout, a deactivation, a role change or an admin password reset — there is no server-side access-token revocation/blocklist. The default lifetime was lowered to **15 minutes** (**USRV-09**) to shrink that window; full per-request revocation (a `SecurityStamp` claim revalidated against the database on every request) is deliberately **not** implemented because it would reintroduce a database read per authenticated request and undo the stateless-JWT model — hosts needing immediate revocation should register a `JwtBearerEvents.OnTokenValidated` stamp check themselves. Logout is `[AllowAnonymous]` and revokes only the single presented token by hash — possession of the (unguessable, hashed-at-rest) token is itself the authority to revoke it (**USRV-11**, by design). Revoked/expired `RefreshToken` rows are not automatically purged; they accumulate in the table over time (see [Upgrade / Breaking Behavior](#upgrade--breaking-behavior) for a one-time cleanup option).

#### Extension points

**`protected virtual IReadOnlyList<string> RolesToSeed`** — the role names created by `EnsureRolesExist` during `setup`. Default: `["Admin", "User"]`. Override to supply additional roles:

```csharp
protected override IReadOnlyList<string> RolesToSeed => ["Admin", "Member", "Viewer"];
```

**`protected virtual Task OnAfterUserCreatedAsync(TUser user, SetupRequest request)`** — called in the `setup` flow after the user is created and assigned the `Admin` role, before token generation. Override to run host-specific initialization such as creating a first household:

```csharp
protected override async Task OnAfterUserCreatedAsync(TrackventoryUser user, SetupRequest request)
{
    var household = new Household { Name = request.HouseholdName ?? user.DisplayName };
    DbContext.Households.Add(household);
    await DbContext.SaveChangesAsync();
}
```

**`protected Task<LoginResponse> GenerateTokenResponse(TUser user, Guid? familyId = null)`** — builds the access + refresh token pair and persists the refresh token. Calls the now-async `TokenService.GenerateAccessToken`. `familyId` is `null` for a fresh login (a new token family is started) and is passed the presenting token's `FamilyId` from `refresh` so the rotated token stays in the same family (see [`RefreshToken`](#refreshtoken)).

### `UserControllerBase<TUser>` *(abstract)*

Route: `[Route("api/user")] [Authorize]`

| Method | Route | Body | Returns |
|---|---|---|---|
| `GET` | `me` | — | `UserProfile` (Id, Email, DisplayName, ThemePreference, Language, Role) |
| `PUT` | `me/settings` | `UpdateUserSettingsRequest` | `NoContent` |

The current user is resolved via `User.FindFirstValue(ClaimTypes.NameIdentifier)`.

### Admin user CRUD

`MapBlazorBaseUserAdminEndpoints(path = "users", roles = "Admin")` maps the 6 BlazorBase.CRUD endpoints for `UserModel` at `/api/base/users`, scoped to the `Admin` role:

| Method | Route |
|---|---|
| `POST` | `/api/base/users/query` |
| `GET` | `/api/base/users/{id}` |
| `GET` | `/api/base/users/count` |
| `POST` | `/api/base/users` |
| `PATCH` | `/api/base/users/{id}` |
| `DELETE` | `/api/base/users/{id}` |

The endpoints route through `UserDataProvider<TUser>` which wraps `UserManager<TUser>`.

---

## Configuration: JwtSettings

| Key | Type | Default | Description |
|---|---|---|---|
| `JwtSettings:Secret` | string | **required** | Symmetric signing key (HMAC SHA-256) |
| `JwtSettings:Issuer` | string | — | `iss` claim |
| `JwtSettings:Audience` | string | — | `aud` claim |
| `JwtSettings:ExpirationMinutes` | int | 15 | Access-token lifetime (lowered from 60 — see USRV-09 in [Upgrade / Breaking Behavior](#upgrade--breaking-behavior)) |
| `JwtSettings:RefreshExpirationDays` | int | 30 | Refresh-token lifetime |
| `JwtSettings:RateLimit:PermitLimit` | int | 10 | Max `login`/`refresh`/`setup` requests per window per client IP |
| `JwtSettings:RateLimit:WindowSeconds` | int | 60 | Rate-limit window length in seconds |

`ClockSkew = 30s` on the JwtBearer middleware. `RandomNumberGenerator` produces refresh tokens — never reuse, always rotate.

Generate a strong secret:
```powershell
[Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes([Guid]::NewGuid().ToString("N") + [Guid]::NewGuid().ToString("N")))
```

> Treat `Secret` as sensitive: store in user-secrets in development and a key vault / environment variable in production.

---

## Configuration: DevelopmentAuthentication

Lets a developer run the app **without signing in**. With this switched on, the client asks the
server for a session at startup and lands on the app instead of the login form. Switch the roles
and restart to try a feature as a different role.

| Key | Type | Default | Description |
|---|---|---|---|
| `DevelopmentAuthentication:Enabled` | bool | `false` | Opts in. **Throws at startup outside the Development environment** |
| `DevelopmentAuthentication:Email` | string | `developer@localhost` | The account to sign in as; created on first use |
| `DevelopmentAuthentication:DisplayName` | string | `Development User` | Display name of that account |
| `DevelopmentAuthentication:Roles` | string[] | `["Admin"]` | Roles the account holds. **Synchronized** on every sign-in — roles removed here are removed from the account |

Belongs in `appsettings.Development.json` only:

```json
{
  "DevelopmentAuthentication": {
    "Enabled": true,
    "Email": "developer@localhost",
    "DisplayName": "Development User",
    "Roles": [ "Admin" ]
  }
}
```

Server registration — safe to call unconditionally, the feature stays off unless configured:

```csharp
builder.Services.AddBlazorBaseDevelopmentAuthentication<MyUser>(builder.Configuration, builder.Environment);
```

Client side (see [`BlazorBase.User`](BlazorBase.User.md)):

```csharp
var host = builder.Build();
await host.Services.UseBlazorBaseDevelopmentSessionAsync();
await host.RunAsync();
```

**This is a real sign-in, not a bypass.** The account is an ordinary Identity user, the token is an
ordinary access token, and authorization runs exactly as it does in production — so what a
developer exercises is what ships. The account gets a random password nobody holds, so it cannot
be reached through the login form.

**One account, one token.** The account is looked up by `Email`, so repeated sign-ins reuse it
rather than piling up users. A token is issued on every app start, though — every reload, every
server restart — so the account's earlier refresh tokens are deleted with each sign-in; otherwise
the table would collect a row per reload and reach the hundreds over a day. Other users' tokens are
untouched. The trade-off: a second browser session against the same server loses its refresh token
when the first one starts, and signs in again on its next reload.

Three independent guards keep it out of production:

1. Registration **throws at startup** when `Enabled` is set outside Development — a failed
   deployment beats an unauthenticated production app.
2. The endpoint re-checks the environment per request and answers `404` otherwise, so a
   configuration reload cannot open it either.
3. `GET api/auth/status` reports `DevelopmentAuthenticationEnabled: false` everywhere else, so no
   client will even try.

---

## Upgrade / Breaking Behavior

**USRV-01 — hashed refresh-token storage & single-use rotation.** Deploying this change invalidates every existing refresh token: previously `RefreshToken.Token` stored the raw token value, and lookups now hash the incoming token before comparing it against the stored value, so an old raw-value row will never match a hashed lookup again.

- **Every logged-in user must re-authenticate (log in again) after deploying this version** — their existing refresh token silently stops working the next time `refresh` or `logout` is called, and they fall back to `/login`.
- **No EF migration is required.** The `RefreshToken.Token` column shape is unchanged (`[MaxLength(500)]`, unique index) — a SHA-256 hex hash is 64 characters, well within the existing column.
- **Optional cleanup:** old rows written before the upgrade become permanently dead (their raw-value `Token` can never hash-match again) but are not automatically deleted. Hosts that want to reclaim the space can run a one-time purge of pre-upgrade `RefreshTokens` rows (e.g. all rows with `CreatedAt` before the deployment timestamp) — this is optional housekeeping, not required for correctness.
- Also bundled in this change: refresh rotation is now single-use/atomic (a replayed or concurrently-lost refresh token returns `401` instead of being accepted), `logout` is `[AllowAnonymous]`, and an admin password reset revokes the target user's refresh tokens. See the [Controllers](#controllers) section for details.

**Medium/low security follow-up (work item #112).** Behavior/config changes in this batch:

- **USRV-04 — refresh-token theft detection.** New `RefreshToken.FamilyId` column (**requires an EF migration** — see [Apply migrations](#6-apply-migrations)). Replaying an already-rotated token now revokes the whole family and returns `401`.
- **USRV-05 — JWT secret minimum length.** Startup now throws if `JwtSettings:Secret` is shorter than 32 bytes. Hosts using a short development secret must lengthen it.
- **USRV-07 — login anti-enumeration.** Unknown email, wrong password and lockout now return one generic `401` message with timing normalized; the distinct "account locked" message is gone.
- **USRV-08 — `ValidateAccessToken` now rejects expired tokens.** A host that called this method directly expecting expired tokens to pass must adjust.
- **USRV-09 — default access-token lifetime lowered 60 → 15 minutes.** Override `JwtSettings:ExpirationMinutes` to restore the old value if required.
- **USRV-10 — refresh honors lockout.** A locked-out user can no longer refresh.
- **USRV-12 — rate limiting.** `AddBlazorBaseUserServer` registers a `"BlazorBaseAuth"` fixed-window policy on `login`/`refresh`/`setup`; **add `app.UseRateLimiter()`** to enforce it. Tune `JwtSettings:RateLimit:*`.

---

## REST API Reference

### Status / Setup

```http
GET /api/auth/status
200 OK   { "hasUsers": false }

POST /api/auth/setup
{
  "email": "admin@example.com",
  "password": "S3cret-Pass",
  "displayName": "Admin"
}
200 OK   { "accessToken": "...", "refreshToken": "...", "expiresAt": "..." }
409 Conflict   { "message": "System is already set up." }
```

### Login / Refresh / Logout

```http
POST /api/auth/login        { "email": "...", "password": "..." }
200 OK   { "accessToken": "...", "refreshToken": "...", "expiresAt": "..." }

POST /api/auth/refresh      { "refreshToken": "..." }
200 OK   { "accessToken": "...", "refreshToken": "...", "expiresAt": "..." }
401 Unauthorized   { "message": "Invalid or expired refresh token." }   (unknown, expired, or already-revoked/replayed token)

POST /api/auth/logout       { "refreshToken": "..." }
204 No Content   (anonymous — no Authorization header required; succeeds even with an expired access token)
```

### Current user

```http
GET /api/user/me                        (Authorization: Bearer ...)
PUT /api/user/me/settings               { "themePreference": "Dark", "language": "en" }
```

### Admin user CRUD — delegated to BlazorBase.CRUD

All `/api/base/users/*` endpoints conform to the BlazorBase.CRUD contract — see [`BlazorBase.CRUD.md`](BlazorBase.CRUD.md) for query / patch shapes.

---

## Code Examples

### Custom `AppUser` with avatar

```csharp
public class AppUser : BaseUser
{
    [MaxLength(500)]
    public string? AvatarUrl { get; set; }
}
```

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : BaseUserDbContext<AppUser>(options)
{
    public DbSet<Product> Products => Set<Product>();
}
```

### Concrete controllers

```csharp
public class AuthController(
    UserManager<AppUser> users,
    RoleManager<IdentityRole> roles,
    TokenService<AppUser> tokens,
    AppDbContext db,
    IConfiguration config)
    : AuthControllerBase<AppUser>(users, roles, tokens, db, config);

public class UserController(UserManager<AppUser> users)
    : UserControllerBase<AppUser>(users);
```

### Extending claims in the JWT

Implement `IClaimsAugmentor<TUser>` and register it with `AddClaimsAugmentor`. The augmentor runs inside `TokenService.GenerateAccessToken` after the built-in claims are assembled; its return value is appended before signing:

```csharp
public class AppClaimsAugmentor(AppDbContext dbContext) : IClaimsAugmentor<AppUser>
{
    #region Injects
    private readonly AppDbContext DbContext = dbContext;
    #endregion

    public async Task<IEnumerable<Claim>> GetAdditionalClaimsAsync(AppUser user, IList<string> roles)
    {
        var tenantId = await DbContext.Memberships
            .Where(m => m.UserId == user.Id)
            .Select(m => m.TenantId)
            .FirstOrDefaultAsync();

        if (tenantId is null)
            return [];

        return [new Claim("tenant_id", tenantId.ToString())];
    }
}
```

```csharp
// Program.cs
builder.Services.AddClaimsAugmentor<AppUser, AppClaimsAugmentor>();
```

Multiple augmentors can be registered; their claims are all appended. When no augmentor is registered the token's claim set is identical to the previous behavior.

### Seeding the first admin programmatically

```csharp
app.MapPost("/internal/seed-admin", async (
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager) =>
{
    foreach (var role in new[] { "Admin", "User" })
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));

    var admin = new AppUser { UserName = "admin@example.com", Email = "admin@example.com", DisplayName = "Admin" };
    var result = await userManager.CreateAsync(admin, "S3cret-Pass!");
    if (!result.Succeeded)
        return Results.BadRequest(result.Errors);

    await userManager.AddToRoleAsync(admin, "Admin");
    return Results.Ok();
}).RequireHost("localhost");
```
