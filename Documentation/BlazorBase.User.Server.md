# BlazorBase.User.Server

The server-side companion to [`BlazorBase.User`](BlazorBase.User.md). Bundles ASP.NET Core Identity, opaque bearer-token issuance and validation, refresh-token persistence with theft detection, account-lifecycle endpoints (email confirmation, password reset, two-factor authentication), an abstract base for the `/api/auth` and `/api/user` controllers, and a `IBaseDataProvider<UserModel>` that integrates user management into the BlazorBase.CRUD pipeline.

> Target framework: `net10.0` · References `BlazorBase.User` and `BlazorBase.CRUD`.

> **Choosing how tokens reach the client:** the token pair can travel in the response body (bearer,
> the default) or in `HttpOnly` cookies (backend-for-frontend). The choice matters for security and
> is the application's to make — see
> [Authentication token transports](BlazorBase.User.Server.AuthTransports.md), which also covers CSRF
> and both external sign-in entry points.

---

## Table of Contents

- [Architecture & Overview](#architecture--overview)
- [Getting Started](#getting-started)
- [Entities & DbContext](#entities--dbcontext)
- [Access tokens (machine-to-machine)](#access-tokens-machine-to-machine)
- [Services](#services)
- [Controllers](#controllers)
- [Account management: email confirmation, password reset, two-factor](#account-management-email-confirmation-password-reset-two-factor)
- [Choosing a token transport, CSRF, and forwarded headers](#choosing-a-token-transport-csrf-and-forwarded-headers)
- [Configuration: AuthTokens](#configuration-authtokens)
- [Upgrade / Breaking Behavior](#upgrade--breaking-behavior)
- [REST API Reference](#rest-api-reference)
- [Code Examples](#code-examples)

---

## Architecture & Overview

```
┌──────────────────────────────────────────────────────────┐
│  HTTP                                                    │
│  /api/auth/{status,setup,login,login/2fa,refresh,logout} │
│  /api/auth/manage/{confirm-email,forgot-password,...}    │
│  /api/user/me, /api/user/me/settings                     │
│  /api/base/users  (BlazorBase.CRUD endpoints)            │
├──────────────────────────────────────────────────────────┤
│  Controllers / endpoint mappers                          │
│   AuthControllerBase<TUser>                               │
│   UserControllerBase<TUser>                               │
│   AccountEndpointMapper (account-management minimal API) │
│  ── consumer derives concrete controllers (TUser=AppUser)│
├──────────────────────────────────────────────────────────┤
│  Services                                                │
│   AuthTokenIssuer<TUser>   mints/rotates the token pair  │
│   IAuthTokenTransport      bearer or cookie delivery     │
│   UserDataProvider<TUser>  IBaseDataProvider<UserModel>  │
├──────────────────────────────────────────────────────────┤
│  Data                                                    │
│   BaseUser : IdentityUser  (DisplayName, ThemePreference,│
│                              Language, IsActive,         │
│                              CreatedAt)                  │
│   RefreshToken     (Token = SHA-256 hash, UNIQUE index,  │
│                      UserId + FamilyId indexes, FK)      │
│   AccessToken      (TokenHash = SHA-512, Prefix index,   │
│                      optional UserId — see below)        │
│   SetupCompletionMarker (single-row race guard for setup)│
│   BaseUserDbContext<TUser> : IdentityDbContext<TUser>    │
├──────────────────────────────────────────────────────────┤
│  Identity + opaque bearer auth                           │
│   PasswordOptions  (length≥12, digit, lower, upper)      │
│   Identity bearer token (Data-Protection-protected,      │
│                          configured from "AuthTokens")   │
│   BlazorBaseAccessToken  (opt-in, additive scheme)       │
└──────────────────────────────────────────────────────────┘
```

`TUser` is the consumer's concrete user class (e.g. `AppUser`) that inherits from `BaseUser`. `TContext` is the consumer's `DbContext` deriving from `BaseUserDbContext<TUser>`. This generic approach lets the host extend the user with custom properties while keeping the auth pipeline intact.

There is no self-issued JWT anywhere in this framework any more. The access token is an **opaque**
value minted by ASP.NET Core Identity's own bearer-token scheme (`AddBearerToken`): a Data-Protection-
protected ticket, not a signed, inspectable token. That trades away one thing a hand-rolled JWT gave
for free — a client or an intermediary reading claims straight out of the token — for a real gain: no
signing secret exists anywhere to be misconfigured, leaked, or rotated by hand. The key ring behind
Data Protection rotates itself. The unavoidable cost is that only the application that minted the
token, or a farm of instances sharing the same Data Protection key ring, can validate it; there is no
way to hand the token to a third party and have it verify the signature itself. `AuthSessionResponse`
(via `GET /api/auth/session`, see [Authentication token transports](BlazorBase.User.Server.AuthTransports.md))
exists precisely because a client can no longer decode its own token to read its claims.

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
- `IdentityCore<TUser>` with `AddRoles<IdentityRole>().AddEntityFrameworkStores<TContext>().AddDefaultTokenProviders().AddSignInManager().AddClaimsPrincipalFactory<BlazorBaseUserClaimsPrincipalFactory<TUser>>()`
- `Authentication` → Identity's own bearer-token scheme (`AddBearerToken`, scheme name `IdentityConstants.BearerScheme` unless overridden — see [`BlazorBaseUserServerOptions`](#blazorbaseuserserveroptions) below), lifetimes read from `IConfiguration["AuthTokens:*"]`
- `Authorization`
- `AuthTokenIssuer<TUser>` (scoped) — mints and rotates the access/refresh token pair; see [Services](#services)
- `BaseUserDbContext<TUser>` (scoped) — bridged to the host's concrete `TContext` via `serviceProvider.GetRequiredService<TContext>()`
- `IBaseDataProvider<UserModel>` → `UserDataProvider<TUser>`
- `IHttpContextAccessor`
- `IEmailSender<TUser>` → `NoOpEmailSender<TUser>` by default (logs a warning instead of sending; see [`AddBlazorBaseMailingEmailSender`](#account-management-email-confirmation-password-reset-two-factor))
- `RefreshTokenCleanupService<TUser>` — a hosted service that periodically deletes expired/revoked `RefreshToken` rows (see [Services](#services))
- A **rate limiter** with a fixed-window policy named `"BlazorBaseAuth"` (partitioned by client IP) applied to the `login`/`refresh`/`setup` actions. Thresholds come from `AuthTokens:RateLimit:*` (see [Configuration](#configuration-authtokens)). The policy is only enforced once the host adds `app.UseRateLimiter()` to its middleware pipeline — registering the policy without the middleware is harmless.

> **Host requirement:** because `BaseUserDbContext<TUser>` is bridged from `TContext` through the container, the host **must** register `TContext` itself as a scoped service — i.e. via `AddDbContext<TContext>(...)` as shown above. Registering `TContext` with any other lifetime (or not at all) breaks the bridge and `AuthControllerBase`/`UserDataProvider` resolution.

#### DI helper

To add extra claims to every issued token register an `IClaimsAugmentor<TUser>` implementation:

```csharp
// Program.cs — call before AddBlazorBaseUserServer or after, order does not matter
builder.Services.AddClaimsAugmentor<AppUser, AppClaimsAugmentor>();
```

`AddClaimsAugmentor<TUser, TAugmentor>` registers `TAugmentor` as a scoped `IClaimsAugmentor<TUser>`. Calling it multiple times registers multiple augmentors; they run in registration order. Claims are added by `BlazorBaseUserClaimsPrincipalFactory<TUser>`, which builds the claim set once for every sign-in path (a fresh login, a token refresh, and — if the cookie transport is used — the application cookie), so an augmentor's claims land on every one of them identically.

### 4. Derive controllers

`AuthControllerBase` and `UserControllerBase` are `abstract`. Provide thin concrete classes so ASP.NET Core picks them up:

```csharp
[ApiController]
public class AuthController(
    UserManager<AppUser> users,
    SignInManager<AppUser> signInManager,
    RoleManager<IdentityRole> roles,
    AuthTokenIssuer<AppUser> tokenIssuer,
    TwoFactorPendingTokenService<AppUser> pendingTokenService)
    : AuthControllerBase<AppUser>(users, signInManager, roles, tokenIssuer, pendingTokenService);

[ApiController]
public class UserController(UserManager<AppUser> users)
    : UserControllerBase<AppUser>(users);
```

`AuthControllerBase<TUser>`'s constructor takes no `IConfiguration` and no `DbContext` — everything it
needs to mint and validate tokens is behind `AuthTokenIssuer<TUser>`, and the setup-race guard
(`SetupCompletionMarker`, see [Entities & DbContext](#entities--dbcontext)) is resolved per-request
from `HttpContext.RequestServices`, not injected.

### 5. Configure AuthTokens (optional)

There is no signing secret to configure — the access token is protected by ASP.NET Core's own Data
Protection key ring, which the host already has as soon as it calls `AddDataProtection()` (implicit in
most hosting models) and which rotates itself. Everything under `AuthTokens` is optional and defaults
to a working configuration:

> **But do decide where that key ring lives.** The default is the local machine or user profile, which
> a container discards on every redeploy — signing every user out — and which two instances do not
> share, so they reject each other's tokens as sporadic 401s. For a single durable host,
> `PersistKeysToFileSystem` with a stable `SetApplicationName` is enough; for several instances or for
> encryption at rest, see
> [BlazorBase.DataProtection.AzureKeyVault.md](BlazorBase.DataProtection.AzureKeyVault.md).

```json
{
  "AuthTokens": {
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 30,
    "CleanupIntervalHours": 24,
    "RateLimit": {
      "PermitLimit": 10,
      "WindowSeconds": 60
    }
  }
}
```

See [Configuration: AuthTokens](#configuration-authtokens) for what each key does and where its
default comes from.

> **Enforce the rate limiter:** to apply the `"BlazorBaseAuth"` policy registered by `AddBlazorBaseUserServer`, add `app.UseRateLimiter();` **after `app.UseRouting()` and before the endpoint mapping** — the policy is attribute-based (`[EnableRateLimiting]`), so routing must have selected the endpoint before the limiter runs; placing it before `UseRouting` silently applies nothing. `"BlazorBaseAuth"` is a **reserved policy name** — a host may still call `AddRateLimiter` for its own *differently* named policies. The partition key is the client IP (IPv6 is masked to its /64 prefix so an attacker cannot get a fresh bucket per address); the request body is not available at partition time, so per-email keying is intentionally not used. Hosts behind a reverse proxy/CDN **must** call [`AddBlazorBaseForwardedHeaders`](#behind-a-reverse-proxy) before `UseRateLimiter` so the real client IP is seen — otherwise all clients collapse into one bucket (a self-inflicted `429` for everyone) and per-client isolation is lost, and a host that forgets this behind a real proxy is warned about it at startup.

### 6. Apply migrations

Identity tables plus `RefreshTokens` and `SetupCompletionMarkers` come from `BaseUserDbContext`:

```bash
dotnet ef migrations add InitialUser
dotnet ef database update
```

> **Migration required on upgrade:** applications that migrated through the earlier `JwtSettings`-based
> version and are now moving to this one pick up two new tables/columns in one step —
> `SetupCompletionMarker` (the setup-race guard) and `RefreshToken.FamilyId` plus its foreign key to
> the user table. See [Upgrade / Breaking Behavior](#upgrade--breaking-behavior) for the full list and
> the migrate-before-deploy ordering that applies to it.

---

## Entities & DbContext

### `BaseUser : IdentityUser`

```csharp
[MaxLength(100)] public string DisplayName { get; set; } = "";
[MaxLength(20)]  public string ThemePreference { get; set; } = "System";
[MaxLength(10)]  public string Language { get; set; } = "en";
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
    public Guid     Id            { get; set; }
    [Required, MaxLength(500)]
    public string   Token         { get; set; } = "";
    [Required]
    public string   UserId        { get; set; } = "";
    public Guid     FamilyId      { get; set; }
    [Required, MaxLength(200)]
    public string   SecurityStamp { get; set; } = "";
    public DateTime ExpiresAt     { get; set; }
    public DateTime CreatedAt     { get; set; }
    public bool     IsRevoked     { get; set; }
}
```

`Token` stores the **SHA-256 hex hash** of the refresh token, not the raw value — a 64-character hex string, well within `MaxLength(500)` and the unique index. The raw token is only ever handed to the client once, in the `LoginResponse` returned by `setup`/`login`/`refresh`/`login/2fa`; the server hashes an incoming token before every lookup (`refresh`, `logout`, both inside `AuthTokenIssuer<TUser>`) and never stores or logs the raw value. This means a leaked database dump does not expose usable refresh tokens.

`FamilyId` groups a login and all refresh tokens rotated from it into one *token family*. A fresh `login`/`setup` starts a new family; each rotation on `refresh` inherits the presenting token's `FamilyId`. If an **already-rotated (revoked) token is replayed** — the classic signature of a stolen-and-then-legitimately-rotated token — `refresh` revokes the **entire family**, cutting off the thief and the victim alike and forcing a clean re-login (the OAuth 2.0 Security BCP reuse-detection scheme for public clients).

`SecurityStamp` captures the user's Identity security stamp at the moment the token was minted and is re-checked, the same way `SignInManager<TUser>.ValidateSecurityStampAsync` re-checks a cookie session, on every `refresh`. A stamp rotated elsewhere — a password change, an admin-initiated sign-out — invalidates every refresh token minted before it without needing to touch each row: the check is against the *user's current* stamp, not the token's own state.

There is a real foreign key from `RefreshToken.UserId` to the user table with `OnDelete(DeleteBehavior.Cascade)` (declared in `BaseUserDbContext<TUser>.OnModelCreating`), so a deleted user cannot leave orphaned refresh-token rows behind.

### `BaseUserDbContext<TUser> : IdentityDbContext<TUser>`

```csharp
public abstract class BaseUserDbContext<TUser>(DbContextOptions options) : IdentityDbContext<TUser>(options)
    where TUser : BaseUser
{
    public DbSet<AccessToken> AccessTokens => Set<AccessToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SetupCompletionMarker> SetupCompletionMarkers => Set<SetupCompletionMarker>();
}
```

`SetupCompletionMarker` is a single-row table (fixed primary key `1`) inserted, inside the same
transaction as the new admin account, the moment `POST api/auth/setup` succeeds. `UserManager.Users.AnyAsync()`
alone cannot be the setup guard: two concurrent requests can both observe no users and both proceed to
create one. Making the marker's primary key the guard turns that race into a database-enforced
constraint violation — exactly one request's insert can succeed, regardless of how many application
instances are running. Every deriving `TContext` therefore needs a migration for this table even if
it never otherwise changes its schema; see [Upgrade / Breaking Behavior](#upgrade--breaking-behavior).

---

## Access tokens (machine-to-machine)

A refresh token belongs to a browser session; an **access token** is the credential a *program* carries — an MCP server, a CI job, a script. It is long-lived, hashed at rest, individually revocable, and authenticated by its own scheme so it never competes with the token a browser session uses.

### Registration

```csharp
builder.Services.AddBlazorBaseAccessTokens<AppDbContext>(builder.Configuration);
```

Registers the scheme **additively** — the app's default (Identity bearer) scheme is untouched. Endpoints opt in one at a time:

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

The same re-read also checks the **owner**. A token created with a `userId` is only valid while that
account still exists and has `IsActive == true`. Deactivating a user therefore kills their machine
tokens on the next call, with no separate revocation step. A token created without a `userId` has no
owner to check and is unaffected. The owner is looked up through the root `BaseUser` entity type in
the EF model, so a host that derives further user types with table-per-hierarchy still resolves the
base row.

### Claims on the token principal, and the owner's roles

An authenticated access-token principal carries `AccessTokenClaimTypes.TokenId`, `ClaimTypes.Name`
(the token's name) and, for a token with an owner, `ClaimTypes.NameIdentifier` (the owner's id). It
carries **no roles** by default: a machine token gets exactly what an endpoint grants it, never more
by accident. Extra claims come from `IAccessTokenClaimsAugmentor` implementations registered with
`AddAccessTokenClaimsAugmentor<TAugmentor>()`, applied in registration order.

When a token is meant to act with its owner's rights — an MCP endpoint guarded by
`RequireRole("Admin", "User")`, for example — opt into the built-in augmentor:

```csharp
builder.Services.AddBlazorBaseAccessTokens<AppDbContext>(builder.Configuration);
builder.Services.AddAccessTokenOwnerRoleClaims<AppUser>();
```

`AccessTokenOwnerRoleClaimsAugmentor<TUser>` reads the owner's current roles through
`UserManager<TUser>` on every request and adds them as `ClaimTypes.Role` claims, so removing a role
limits the token on its next call. A token without an owner, or whose owner was deleted, gets no roles;
a deactivated owner's tokens are already rejected by validation (above). `AccessTokenOwnerRoleClaimsTests`
pins the role check, the immediate effect of removing a role, and the ownerless case.

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

Failed attempts incur a per-client exponential back-off keyed on the **transport-level remote address**, never on `X-Forwarded-For`: a caller able to choose its own bucket could reset the delay at will. Behind a reverse proxy, call `AddBlazorBaseForwardedHeaders` and `app.UseForwardedHeaders()` so that address is the real client (see [forwarded headers](BlazorBase.User.Server.AuthTransports.md#behind-a-reverse-proxy--forwarded-headers)). The state is per-process, so several instances give an attacker one ladder each — a brake on guessing, not a global rate limit.

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

A DI seam for injecting extra claims into every issued access token. Implement this interface to emit claims (e.g. `household_id`) without touching `BlazorBaseUserClaimsPrincipalFactory<TUser>` itself:

```csharp
public interface IClaimsAugmentor<TUser> where TUser : BaseUser
{
    Task<IEnumerable<Claim>> GetAdditionalClaimsAsync(TUser user, IList<string> roles);
}
```

Register implementations with `AddClaimsAugmentor<TUser,TAugmentor>(services)` (see [DI helper](#di-helper)). Multiple augmentors are applied in registration order. When none are registered the token's claim set is identical to the pre-augmentor behavior.

### `AuthTokenIssuer<TUser>`

Mints and rotates the access/refresh token pair. Where the deleted `TokenService<TUser>` built and
validated a self-signed JWT by hand, `AuthTokenIssuer<TUser>` builds on ASP.NET Core Identity's own
bearer-token infrastructure: the access token is produced by protecting an `AuthenticationTicket`
through the bearer scheme's own `BearerTokenOptions.BearerTokenProtector` (Data Protection underneath),
so validating it later is Identity's job, not this framework's.

| Method | Purpose |
|---|---|
| `IssueTokenPairAsync(principal, familyId?)` | Mints a fresh pair for `principal` and persists the refresh token's hash. `familyId` is omitted for a brand-new login (`setup`, `login`, `login/2fa`, `development-login`) and starts a new token family. |
| `FindAsync(presentedToken)` | Looks up a refresh token by its SHA-256 hash without mutating it, returning a `StoredRefreshToken` (id, user id, family id, security stamp, expiry, revoked flag) or `null` when unknown. |
| `IsExpired(stored)` | Whether a looked-up token's `ExpiresAt` has passed, compared against the injected `TimeProvider`. |
| `ValidateSecurityStampAsync(stored)` | Re-checks the token's captured security stamp against the user's *current* one — the same check `SignInManager<TUser>.ValidateSecurityStampAsync` makes for a cookie session — and returns the `TUser` when it still matches, or `null` when the stamp has moved on (password change, sign-out-everywhere). |
| `RevokeFamilyAsync(userId, familyId)` | Revokes every unrevoked token in a family — the routine case (logout, password change). |
| `RevokeFamilyAfterReuseDetectedAsync(userId, familyId)` | Same, but also logs a warning — the reuse-detected case, when an already-revoked token was presented again. |
| `RotateAsync(stored, principal)` | Atomically revokes `stored` (a conditional `WHERE Id = … AND !IsRevoked` update) and issues its replacement in the same family inside one database transaction, or returns `null` when a concurrent refresh already revoked it first. |

There is no `GenerateAccessToken`/`ValidateAccessToken` pair to call directly any more: validating a
bearer token is the concern of Identity's own bearer-token authentication handler, invoked
automatically for every authenticated request, not something a caller does by hand.

### `TwoFactorPendingTokenService<TUser>` and `ITwoFactorPendingTokenStore`

Between `POST api/auth/login` on an account with two-factor enabled and `POST api/auth/login/2fa`, the
server hands the caller a short-lived opaque **pending token** instead of a session.
`TwoFactorPendingTokenService<TUser>` mints and validates it; it is data-protected, carries the user id
and a random single-use id, and lives **5 minutes**.

The token is **single use**. It is consumed only after the submitted code has been verified, so a
wrong code does not burn it, and the consume result decides the response — two requests racing with
the same pending token cannot both mint a session.

| Method | Purpose |
|---|---|
| `Protect(user)` | Mints the pending token: the user id, a random single-use id, and a 5-minute expiry, data-protected. |
| `UnprotectAsync(pendingToken, cancellationToken)` | Returns the user id, or `null` when the token is malformed, expired, or has already been consumed. |
| `ConsumeAsync(pendingToken, cancellationToken)` | Marks the token spent. Returns `true` only for the first caller; a second caller gets `false` and must be rejected. |

`ITwoFactorPendingTokenStore` is what remembers which pending-token ids are spent:
`IsConsumedAsync(tokenId, cancellationToken)` and `TryConsumeAsync(tokenId, expiresUtc, cancellationToken)`.
The default is `InMemoryTwoFactorPendingTokenStore`, registered
`TryAddSingleton<ITwoFactorPendingTokenStore, InMemoryTwoFactorPendingTokenStore>()`, so a host
registration wins regardless of order.

**It is per process.** On more than one instance, a captured pending token can be replayed against an
instance that has not seen it spent. A multi-instance deployment must supply a distributed
implementation.

### `RefreshTokenCleanupService<TUser>`

A `BackgroundService`, registered automatically by `AddBlazorBaseUserServer`, that periodically
deletes every `RefreshToken` row that is expired or revoked. The interval comes from
`AuthTokens:CleanupIntervalHours` (default 24, see [`AuthTokenDefaults`](#configuration-authtokens)).
This closes a defect the deleted table had from the start — nothing ever removed a row once it
stopped being useful, so it grew without bound. A cleanup pass that fails (a transient database blip)
is logged and swallowed rather than propagated, so a cleanup hiccup can never take the host down with
it.

### `UserDataProvider<TUser>` and `UserDataProvider<TUser, TModel>`

`UserDataProvider<TUser, TModel>` implements `IBaseDataProvider<TModel>` for a `TModel` that implements `IBaseUser`;
`UserDataProvider<TUser>` is the one for the framework `UserModel`. An application with its own user model (more
columns, its own `[CrudAccess]` rules) derives from the generic one and overrides:
- `MapToModelAsync(TUser)` — fills the extra columns;
- `ApplySort(source, ordered, sort)` — orders by a column the provider does not know (return `ordered` to ignore it);
- `PatchAsync` — virtual, for side effects of a change.

Register it as `IBaseDataProvider<TModel>` after the assembly scan of the host. Hosts that map their own model do not call
`MapBlazorBaseUserAdminEndpoints`, whose model carries the class rule `[CrudAccess("Admin", "RIMD")]`.

Hosts that expose `UserDataProvider<TUser>`:
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
- **`PatchAsync`** — switches over allowed keys: `DisplayName`, `Email`, `IsActive` (a boolean, or the JSON boolean of a patch that came over HTTP; setting it to `false` rotates the security stamp, so the live bearer tokens, refresh tokens and cookie sessions of the account end at their next stamp validation), `Role` (re-assigns role), `Password` (resets via `GeneratePasswordResetTokenAsync`, then **revokes all of that user's active refresh tokens** so existing sessions cannot continue using the old password's grant).
- **`DeleteAsync`** — refuses to delete the currently authenticated user. After a successful delete it invokes every registered `IUserDeletionHandler` (see below) so hosts can clean up per-user external resources.

The `IHttpContextAccessor` dependency is what makes the self-delete check work.

#### `IUserDeletionHandler` (user-deletion seam)

An optional extension point in `Lifecycle/IUserDeletionHandler.cs`: `Task OnUserDeletedAsync(string userId, CancellationToken)`. `UserDataProvider<TUser>` takes an injected `IEnumerable<IUserDeletionHandler>` (empty by default — behavior unchanged for hosts that register none) and calls each handler **once, after** the Identity row is successfully deleted. Handlers are best-effort and must not throw for recoverable failures. Register one per external resource, e.g. `services.AddScoped<IUserDeletionHandler, MyExternalTokenCleanupHandler>()`.

---

## Controllers

### `AuthControllerBase<TUser>` *(abstract)*

Route: `[Route("api/auth")]`

Constructor: `AuthControllerBase<TUser>(UserManager<TUser> userManager, SignInManager<TUser> signInManager, RoleManager<IdentityRole> roleManager, AuthTokenIssuer<TUser> tokenIssuer, TwoFactorPendingTokenService<TUser> pendingTokenService)`. No `IConfiguration`, no `DbContext` — the setup-race guard is resolved per request from `HttpContext.RequestServices` instead.

| Method | Route | Auth | Body | Returns |
|---|---|---|---|---|
| `GET` | `status` | anon | — | `AuthStatusResponse { HasUsers, DevelopmentAuthenticationEnabled }` |
| `POST` | `setup` | anon | `SetupRequest` | `LoginResponse` (creates first user, assigns Admin) |
| `POST` | `login` | anon | `LoginRequest` | `LoginResponse`, or `202 Accepted` with a `TwoFactorRequiredResponse` when the account has two-factor enabled |
| `POST` | `login/2fa` | anon | `TwoFactorLoginRequest` | `LoginResponse` — completes a login `login` answered with `202 Accepted` |
| `POST` | `refresh` | anon | `RefreshTokenRequest` | `LoginResponse` (atomically revokes the presented token, issues a new one) |
| `POST` | `logout` | anon | `RefreshTokenRequest?` (body optional) | `NoContent` (revokes the presented refresh token's family; the token itself is the credential; without a token it ends the cookie session only) |
| `POST` | `development-login` | anon | — | `LoginResponse` for the configured development account — **`404` unless the Development environment has it switched on** |

Behavior details:
- `setup` returns `409 Conflict` if any user already exists, guarded against a concurrent request by `SetupCompletionMarker`'s primary key rather than by the `AnyAsync()` check alone (see [Entities & DbContext](#entities--dbcontext)).
- `login` checks `IsActive`; deactivated users get `401 Unauthorized`. **Anti-enumeration:** an unknown email, a wrong password and a locked-out account all return the *same* generic `401` body (`"Invalid email or password."`), and the unknown-email path performs an equivalent password hash so response timing does not reveal whether an account exists. The lockout counter is still enforced; it just no longer surfaces a distinct "locked" message. A two-factor-enabled account instead gets `202 Accepted` with a short-lived, opaque `PendingToken` (minted by `TwoFactorPendingTokenService<TUser>` under its own Data Protection purpose, so it can never be presented as an access or refresh token) that `login/2fa` exchanges for the real token pair once the code — or a recovery code — checks out. A wrong code counts toward the same lockout counter as a wrong password.
- `refresh` looks up the token by its **SHA-256 hash** and rejects it with `401 Unauthorized` when unknown, expired, or replayed. It re-validates the security stamp captured at issuance against the user's current one, and rejects a refresh for a **locked-out** or **deactivated** user. Rotation is **single-use and atomic**: the presented token is revoked with a conditional update (`WHERE Id = … AND !IsRevoked`) inside the same database transaction that issues the new token pair; if the conditional update affects zero rows — because the token was already revoked, e.g. by a concurrent/replayed refresh — the whole request fails with `401 Unauthorized` instead of silently issuing a second token pair. **Theft detection:** replaying an *already-rotated* token revokes the whole token family (all tokens sharing its `FamilyId`) and logs a warning, so a stolen token that has since been rotated cannot be used and the legitimate session is also forced to re-login. Never touches the user's security stamp itself — rotating a token must not sign the user out of every other session.
- `logout` is `[AllowAnonymous]`: presenting a valid refresh token is itself the authority to revoke it, so logout succeeds even when the caller's access token has already expired. It revokes the presented token's whole family; other sessions/tokens for the same user are untouched. Logging out with an unknown/already-revoked token still returns `204 No Content` (idempotent).
- Both `refresh` and `logout` additionally ask the active `IAuthTokenTransport.IsRequestIntentionalAsync` and answer `403 Forbidden` when it says no — the check that keeps a cross-site page from rotating or revoking a visitor's session under the cookie transport (the bearer transport always answers `true`; see [Authentication token transports](BlazorBase.User.Server.AuthTransports.md)).
- `EnsureRolesExist` creates all roles in `RolesToSeed` on first setup.

**Residual limitations:** none, for the access token's own revocation window - see
[Access-token security-stamp revalidation](#access-token-security-stamp-revalidation) below. `logout`
still only revokes the *refresh* token family by design (see above); `RefreshTokenCleanupService<TUser>`
purges revoked/expired `RefreshToken` rows periodically, so they no longer accumulate unbounded (see
[Services](#services)).

#### Access-token security-stamp revalidation

`BlazorBaseAccessTokenStampValidationMiddleware` (`BlazorBase.User.Server.Security`) re-checks a bearer
access token's captured security-stamp claim against the user's current one, via
`SignInManager<TUser>.ValidateSecurityStampAsync`, on every authenticated bearer request by default - a
password reset, an admin deactivation-adjacent stamp rotation, or an explicit sign-out-everywhere now
invalidates an already-issued access token immediately, closing the window the previous revision of
this document described as a deliberate, unimplemented trade-off. It runs only for a principal
authenticated by the framework's own bearer scheme; a cookie session is already covered by Identity's
own `SecurityStampValidator`, and a machine access token (`AccessTokenAuthenticationDefaults.AuthenticationScheme`)
is already re-checked against the database, including its owner's `IsActive` flag, by
`AccessTokenService`.

Call `app.UseBlazorBaseAccessTokenStampValidation()` after `app.UseAuthentication()` and before
`app.UseAuthorization()`. This is required whenever `AddBlazorBaseUserServer` claims the authentication
default (`BlazorBaseUserServerOptions.ClaimDefaultScheme`, true unless the host opted out) and
revalidation has not been disabled by configuration - an `IStartupFilter` throws on every request until
the call is made, the same shape as `app.UseBlazorBaseCsrf()`'s own guard. A host that dispatches its
own authentication schemes (`ClaimDefaultScheme = false`) has declared it manages that wiring itself
and is exempt from the guard.

`AuthTokens:SecurityStampValidationInterval` (a `TimeSpan`, default `00:00:00`) trades freshness for
database load: zero re-checks on every request (one indexed primary-key read, the same cost
`AccessTokenService` already pays for every machine token); a positive value caches the last successful
check per user for that long before checking again; a negative value disables the middleware entirely,
reproducing the previous behavior exactly.



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

**`protected Task<LoginResponse> GenerateTokenResponse(TUser user)`** — builds the caller's `ClaimsPrincipal` via `SignInManager.CreateUserPrincipalAsync`, mints a fresh access/refresh token pair through `AuthTokenIssuer<TUser>.IssueTokenPairAsync` (always starting a new token family — used for `setup`, `login`, `login/2fa` and `development-login`, never for `refresh`, which calls `RotateAsync` directly to stay in the presented token's family), and writes the pair through the active `IAuthTokenTransport` when an `HttpContext` is available.

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

## Account management: email confirmation, password reset, two-factor

`AccountEndpointMapper` maps the account-lifecycle capability Identity's own token providers already
support but the framework never exposed until this rebuild: email confirmation, self-service password
reset, and TOTP-based two-factor enrolment. It is built entirely on `UserManager`'s own token
generation and verification, not a hand-rolled equivalent.

```csharp
app.MapBlazorBaseAccountEndpoints<AppUser>();   // → api/auth/manage/*, rate-limited by "BlazorBaseAuth"
```

See [REST API Reference](#account-management--apiauthmanage) for every route. A few behaviors worth
calling out explicitly:

- **`resend-confirmation` and `forgot-password` are anti-enumeration by construction:** both answer
  `202 Accepted` identically whether the address is known, already confirmed, neither, or the send
  behind it failed outright - a distinguishable response, in body, status code or latency, would let
  a caller enumerate registered emails one probe at a time. Both handlers wrap the user lookup, token
  generation and dispatch in a `try`/`catch` that logs and swallows any exception, so a failing mail
  transport cannot turn the known-address branch into a `500` while the unknown-address branch still
  returns `202`. An identical body is not enough on its own, though: only the known-address branch
  does the extra lookup and token work, and that is measurably slower than the branch that does
  nothing. Both handlers pad the response up to a randomised floor
  (`AccountManagementOptions.TimingFloorMinimumMilliseconds`/`TimingFloorMaximumMilliseconds`,
  default 750–1250 ms) so the *observed* duration is the floor either way, with the real work
  disappearing underneath it. The target is drawn per-request from `RandomNumberGenerator`, not
  `Random.Shared`, so there is no fixed constant an attacker could calibrate against by averaging
  many requests. Set both values to `0` to disable the padding, which is what an automated test host
  should do to stay fast. With the default background mail dispatch (see
  [`BlazorBase.Mailing`](BlazorBase.Mailing.md)'s `EmailDispatchMode`), the send itself is off the
  response path entirely - the floor only needs to cover the user lookup and token generation that
  remain on it. A host that switches to `EmailDispatchMode.Inline` puts the send back on the response
  path and must raise the floor to cover its own SMTP latency again.
- **`reset-password` is the one endpoint in this group that reports failure honestly.** The reset
  token *is* the credential at that point, so saying it is invalid or expired tells the caller only
  something about a token already in their hands, not about the account. Staying silent instead, as
  the other endpoints do, would answer an expired link with success and leave the password unchanged.
- **Two-factor enrolment is a two-step handshake**, matching how every authenticator app expects it:
  `GET 2fa` returns an `otpauth://` URI (only while two-factor is not yet enabled) for the client to
  render as a QR code, and `2fa/enable` requires the caller to prove they scanned it by submitting a
  currently-valid code before enrolment actually takes effect and recovery codes are issued. Disabling
  two-factor (`2fa/disable`) requires the same proof in reverse - a TOTP code or a recovery code - so
  that a session obtained without the second factor cannot remove it; it also resets the authenticator
  key on success, so re-enabling always starts a fresh enrolment rather than reusing a key the account
  may no longer control. `2fa/recovery-codes` requires the same proof before minting a fresh batch.
- **Three of those operations rotate the caller's own security stamp** - `GET 2fa` the first time it
  generates the authenticator key, `2fa/enable`, and `2fa/disable` - which
  [Access-token security-stamp revalidation](#access-token-security-stamp-revalidation) now enforces
  on the very next bearer request. Each of the three mints a fresh token pair through
  `AuthTokenIssuer<TUser>` and delivers it through the active `IAuthTokenTransport`, exactly the way
  login does, and carries it back as an optional `RefreshedTokens` field on `TwoFactorStatusResponse`,
  `TwoFactorRecoveryCodesResponse` and `TwoFactorDisableResponse` respectively - present only when that
  particular call actually rotated the stamp. `AuthService` on the client adopts it transparently
  through the same `ITokenStorage` it already uses for login, so a page built against the pre-existing
  `IAuthService` methods needs no change of its own to keep its session across these calls; a client
  that ignores the field is signed out on its next request, exactly as it would have been before this
  mechanism existed. Under the cookie transport the pair travels as a rewritten session cookie instead,
  with both token strings in the body blanked, the same as every other response this transport touches.
- **Email delivery goes through `IEmailSender<TUser>`**, registered as `NoOpEmailSender<TUser>` by
  default (logs a warning and sends nothing — a missing mail configuration must not stop the host from
  starting). Call `AddBlazorBaseMailingEmailSender<TUser>()` after `AddBlazorBaseMailing` (see
  [`BlazorBase.Mailing`](BlazorBase.Mailing.md)) to route confirmation and reset links through the
  framework's existing mail pipeline instead of writing a second one.
- **Links embedded in outgoing email point at the client, not this API.** `AccountManagementOptions.ClientBaseUrl`
  is the base URL of the client's own confirm-email and reset-password pages; this API has no
  server-rendered pages of its own to redirect to. `AccountManagementOptions.Issuer` is the name shown
  next to the account entry an authenticator app creates — override it with your own product name
  rather than leaving it at the default `"BlazorBase"`.

```json
{
  "AccountManagement": {
    "Issuer": "OmniLog",
    "ClientBaseUrl": "https://app.example.com",
    "TimingFloorMinimumMilliseconds": 750,
    "TimingFloorMaximumMilliseconds": 1250
  }
}
```

---

## Choosing a token transport, CSRF, and forwarded headers

Three concerns that sit alongside everything above have their own dedicated document, because the
decisions involved carry real security trade-offs that deserve more room than a cross-reference:

- **Bearer vs. cookie delivery** — `IdentityBearerAuthTransport` (the default, for native clients and
  anything that is not same-origin with this API) versus `IdentityCookieAuthTransport` (opt-in via
  `AddBlazorBaseCookieAuthTokens`, for a hosted Blazor WebAssembly application served by this same
  backend).
- **CSRF** — mandatory, default-on middleware (`UseBlazorBaseCsrf()`) the moment the cookie transport
  is in use, built on ASP.NET Core's own `IAntiforgery` rather than a hand-rolled double-submit cookie.
  It must run **after** `app.UseAuthentication()`, because the antiforgery token is bound to the
  caller's identity and can only be validated once that identity has been resolved.
- **Forwarded headers** — `AddBlazorBaseForwardedHeaders` matters as soon as this host sits behind a
  reverse proxy or load balancer: the `"BlazorBaseAuth"` rate limiter and the access-token brute-force
  delay both key on the connection's remote address, which is the proxy's own address, identical for
  every client, until forwarded headers are processed. Loopback stays trusted by ASP.NET Core's
  defaults whatever is configured, so a reverse proxy on the same machine that writes
  `X-Forwarded-For` itself works without configuration. A tunnel or port forward that passes the
  caller's headers through unchanged does not qualify; see the forwarded headers section of
  [Authentication token transports](BlazorBase.User.Server.AuthTransports.md).

See [Authentication token transports](BlazorBase.User.Server.AuthTransports.md) for the full decision
guide, the wiring for each transport, the CSRF checklist, and the external sign-in entry points
(redirect and token-exchange) that both resolve through the same `IExternalUserResolver<TUser>`.

---

## Configuration: AuthTokens

There is no signing secret in this section, and none anywhere in the framework — the access token is
an opaque, Data-Protection-protected value, and the key ring that protects it is ASP.NET Core's own
and rotates itself. Every key below is read once, at `AddBlazorBaseUserServer` registration time, from
`IConfiguration`; the defaults live in `AuthTokenDefaults` and are used whenever the host does not set
the corresponding key.

| Key | Type | Default | Description |
|---|---|---|---|
| `AuthTokens:AccessTokenMinutes` | int | 15 (`AuthTokenDefaults.AccessTokenExpirationMinutes`) | Access-token lifetime |
| `AuthTokens:RefreshTokenDays` | int | 30 (`AuthTokenDefaults.RefreshTokenExpirationDays`) | Refresh-token lifetime |
| `AuthTokens:CleanupIntervalHours` | int | 24 (`AuthTokenDefaults.CleanupIntervalHours`) | How often `RefreshTokenCleanupService<TUser>` sweeps expired/revoked rows |
| `AuthTokens:SecurityStampValidationInterval` | TimeSpan | `00:00:00` (`AuthTokenDefaults.SecurityStampValidationInterval`) | How long a bearer access token's stamp check is cached before re-checking; negative disables re-validation entirely - see [Access-token security-stamp revalidation](#access-token-security-stamp-revalidation) |
| `AuthTokens:RateLimit:PermitLimit` | int | 10 | Max `login`/`refresh`/`setup`/`login/2fa` requests per window per client IP, under the `"BlazorBaseAuth"` policy |
| `AuthTokens:RateLimit:WindowSeconds` | int | 60 | Rate-limit window length in seconds |

`RandomNumberGenerator` produces the 64-byte raw refresh-token value — never reuse one, always rotate
(`AuthTokenIssuer<TUser>.RotateAsync` is what enforces this server-side). Access-token lifetime is
enforced by the bearer-token handler's own `BearerTokenOptions.BearerTokenExpiration`, set from
`AccessTokenMinutes` at registration time; there is no separate clock-skew setting to tune, because an
opaque, server-validated token has no client-side clock to be skewed against.

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
server restart — so each sign-in rotates the account's security stamp, which is what actually
invalidates every refresh token issued before it (the same stamp check `refresh` runs for every
account); the now-unusable rows are revoked in the same step so the table does not keep them around
either. Other users' tokens are untouched. The trade-off: a second browser session against the same
server loses its refresh token when the first one starts, and signs in again on its next reload.

Three independent guards keep it out of production:

1. Registration **throws at startup** when `Enabled` is set outside Development — a failed
   deployment beats an unauthenticated production app.
2. The endpoint re-checks the environment per request and answers `404` otherwise, so a
   configuration reload cannot open it either.
3. `GET api/auth/status` reports `DevelopmentAuthenticationEnabled: false` everywhere else, so no
   client will even try.

---

## Upgrade / Breaking Behavior

**Rebuild onto ASP.NET Core Identity's own bearer-token and Data Protection infrastructure.** The
self-issued HS256 JWT, `TokenService<TUser>`, and the entire `JwtSettings` configuration section
(`Secret`, `Issuer`, `Audience`, `ExpirationMinutes`, `RefreshExpirationDays`, `RateLimit`) are gone.
Tokens are now minted by `AuthTokenIssuer<TUser>` and configured under `AuthTokens` (see
[Configuration: AuthTokens](#configuration-authtokens)). Consequences for an application upgrading
from the `JwtSettings`-based version:

- **Every logged-in user must re-authenticate.** An access token minted under the old scheme cannot
  validate against the new one — there is no signing key to compare it against any more; a client
  falls back to `/login` the next time its old token is rejected.
- **`JwtSettings:*` configuration keys are dead and can be removed.** None of them are read; nothing
  throws if they are left in place, they simply have no effect.
- **`AuthControllerBase<TUser>`'s constructor signature changed** from
  `(UserManager<TUser>, RoleManager<IdentityRole>, TokenService<TUser>, TContext, IConfiguration)` to
  `(UserManager<TUser>, SignInManager<TUser>, RoleManager<IdentityRole>, AuthTokenIssuer<TUser>, TwoFactorPendingTokenService<TUser>)`.
  A host's concrete `AuthController` must update its constructor and the base call to match.
- **Two new entities require an EF migration** against every application's concrete `DbContext`,
  even one that changes nothing else: `SetupCompletionMarker` (the setup-race guard, see
  [Entities & DbContext](#entities--dbcontext)) and `RefreshToken.SecurityStamp` plus the new foreign
  key from `RefreshToken.UserId` to the user table. **Apply the migration to the database before the
  new application version starts serving traffic** — the new code queries both on every
  `setup`/`login`/`refresh` call, so running it against the old schema fails those requests outright.
  ```bash
  dotnet ef migrations add AuthTokenRebuild
  dotnet ef database update
  ```
- **`RefreshToken.FamilyId`** (theft detection: replaying an already-rotated token now revokes the
  whole family and returns `401`) and **hashed-at-rest refresh-token storage** carry over unchanged
  from the previous version; nothing further is needed for either if already migrated.
- **Login anti-enumeration** (unknown email, wrong password and lockout all return one generic `401`
  with normalized timing) and **lockout-aware refresh** (a locked-out user cannot refresh) also carry
  over unchanged.
- **Rate limiting** still registers a `"BlazorBaseAuth"` fixed-window policy on
  `login`/`refresh`/`setup`/`login/2fa`; `app.UseRateLimiter()` is still required to enforce it, and
  the settings that tune it moved from `JwtSettings:RateLimit:*` to `AuthTokens:RateLimit:*`.
- **New capabilities added in the same rebuild** — two-factor authentication, email confirmation,
  password reset, and the cookie/CSRF transport — are additive and require no migration of their own
  beyond the `AccountManagementOptions` configuration they read (see
  [Account management](#account-management-email-confirmation-password-reset-two-factor) and
  [Authentication token transports](BlazorBase.User.Server.AuthTransports.md)).

---

## REST API Reference

### Status / Setup

```http
GET /api/auth/status
200 OK   { "hasUsers": false, "developmentAuthenticationEnabled": false }

POST /api/auth/setup
{
  "email": "admin@example.com",
  "password": "S3cret-Pass!!",
  "displayName": "Admin"
}
200 OK   { "accessToken": "...", "refreshToken": "...", "expiresAt": "..." }
409 Conflict   { "message": "System is already set up." }
```

### Login / Two-factor / Refresh / Logout

```http
POST /api/auth/login        { "email": "...", "password": "..." }
200 OK       { "accessToken": "...", "refreshToken": "...", "expiresAt": "..." }
202 Accepted { "pendingToken": "..." }                                          (two-factor enabled)
401 Unauthorized   { "message": "Invalid email or password." }

POST /api/auth/login/2fa    { "pendingToken": "...", "code": "...", "isRecoveryCode": false }
200 OK   { "accessToken": "...", "refreshToken": "...", "expiresAt": "..." }
401 Unauthorized   { "message": "Invalid or expired two-factor session." }  |  { "message": "Invalid code." }

POST /api/auth/refresh      { "refreshToken": "..." }
200 OK   { "accessToken": "...", "refreshToken": "...", "expiresAt": "..." }
401 Unauthorized   { "message": "Invalid or expired refresh token." }   (unknown, expired, or already-revoked/replayed token)
403 Forbidden   (cookie transport only — the request failed the transport's intentionality check)

POST /api/auth/logout       { "refreshToken": "..." }   (body optional under the cookie transport)
204 No Content   (anonymous — no Authorization header required; succeeds even with an expired access token)
```

### Account management — `api/auth/manage/*`

Mapped separately via `app.MapBlazorBaseAccountEndpoints<AppUser>()`; every route below is rate-limited
by the `"BlazorBaseAuth"` policy. `confirm-email`, `resend-confirmation`, `forgot-password` and
`reset-password` are anonymous; every `2fa` route requires an authenticated caller.

```http
POST /api/auth/manage/confirm-email        { "userId": "...", "token": "..." }
200 OK   |   400 Bad Request   { "message": "Invalid confirmation link." }

POST /api/auth/manage/resend-confirmation  { "email": "..." }
202 Accepted   (identical whether the address is known, already confirmed, or not — anti-enumeration;
                padded to a randomised timing floor so response latency does not leak it either)

POST /api/auth/manage/forgot-password      { "email": "..." }
202 Accepted   (identical whether the address is known — anti-enumeration; same timing-floor padding)

POST /api/auth/manage/reset-password       { "email": "...", "token": "...", "newPassword": "..." }
204 No Content   |   400 Bad Request   { "errors": [ "..." ] }

GET  /api/auth/manage/2fa                              (Authorization: Bearer ...)
200 OK   { "enabled": false, "recoveryCodesRemaining": 0, "authenticatorUri": "otpauth://totp/...", "refreshedTokens": null }

POST /api/auth/manage/2fa/enable           { "code": "..." }
200 OK   { "recoveryCodes": [ "...", "..." ], "refreshedTokens": { "accessToken": "...", "refreshToken": "...", "expiresAt": "..." } }   |   400 Bad Request   { "message": "Invalid code." }

POST /api/auth/manage/2fa/disable          { "code": "..." }
200 OK   { "refreshedTokens": { "accessToken": "...", "refreshToken": "...", "expiresAt": "..." } }   |   400 Bad Request   { "message": "Invalid code." }   |   400 Bad Request   { "message": "Two-factor authentication is not enabled." }

POST /api/auth/manage/2fa/recovery-codes   { "code": "..." }
200 OK   { "recoveryCodes": [ "...", "..." ] }   |   400 Bad Request   { "message": "Invalid code." }   |   400 Bad Request   { "message": "Two-factor authentication is not enabled." }
```

### Current user

```http
GET /api/user/me                        (Authorization: Bearer ...)
PUT /api/user/me/settings               { "themePreference": "Dark", "language": "en" }
```

### Session

```http
GET /api/auth/session
200 OK   { "isAuthenticated": true, "csrfToken": "..." }
```

Mapped via `app.MapBlazorBaseAuthSessionEndpoint()`. Required by default under both token transports —
see [Authentication token transports](BlazorBase.User.Server.AuthTransports.md) for why any BlazorBase
client needs this, and `BlazorBaseUserServerOptions.RequiresAuthSessionEndpoint` for the opt-out on a
host with no BlazorBase client.

The response deliberately omits one claim: the security-stamp claim, whose type is
`IdentityOptions.ClaimsIdentity.SecurityStampClaimType`. It is server-side bookkeeping used to
invalidate sessions; it has no client use, and handing it out widens what a cross-site script can
read. Every other claim on the principal is returned.

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
    SignInManager<AppUser> signInManager,
    RoleManager<IdentityRole> roles,
    AuthTokenIssuer<AppUser> tokenIssuer,
    TwoFactorPendingTokenService<AppUser> pendingTokenService)
    : AuthControllerBase<AppUser>(users, signInManager, roles, tokenIssuer, pendingTokenService);

public class UserController(UserManager<AppUser> users)
    : UserControllerBase<AppUser>(users);
```

### Extending claims in the access token

Implement `IClaimsAugmentor<TUser>` and register it with `AddClaimsAugmentor`. The augmentor runs inside `BlazorBaseUserClaimsPrincipalFactory<TUser>.GenerateClaimsAsync` after the built-in claims are assembled; its return value is appended before the principal is used to mint the token:

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
    var result = await userManager.CreateAsync(admin, "S3cret-Pass!!");
    if (!result.Succeeded)
        return Results.BadRequest(result.Errors);

    await userManager.AddToRoleAsync(admin, "Admin");
    return Results.Ok();
}).RequireHost("localhost");
```
