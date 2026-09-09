# Authentication token transports — choosing between cookies and bearer tokens

`BlazorBase.User.Server` issues the same token pair no matter which client asks for it. What differs
is **how that pair reaches the client**, and that choice is the application's, not the framework's.
Both delivery mechanisms are ASP.NET Core Identity's own: there is no self-issued JWT anywhere in the
framework.

This document is the decision guide and the wiring reference. Read it before adding authentication to
a new application, and before changing it in an existing one.

---

## The three patterns, and which one you are in

The IETF's guidance for browser-based applications describes three shapes. They differ in one thing —
how much a successful cross-site-scripting attack walks away with.

| | Refresh token in browser | Access token in browser | What an XSS (Cross-Site-Scripting) walks away with | Revocation | CSRF (Cross-Site-Request-Forgery) risk |
|---|---|---|---|---|---|
| **1. Backend-for-frontend** | no | no | no token — but it can still act as the user for as long as the session lasts | immediate | yes, must be handled |
| **2. Token-mediating backend** | no | yes, short-lived | the access token, usable until it expires | at expiry | no |
| **3. Browser as OAuth client** | yes | yes | both tokens — access that outlives the session and the browser | at expiry | no |

**Pick by asking one question: does a backend you control serve the client and own the API?**

- **Yes, same origin (or a split `app.`/`api.` deployment on one registrable domain)** → pattern 1.
  Use `AddBlazorBaseCookieAuthTokens`. This is the right default for a hosted Blazor WebAssembly
  application, where the ASP.NET Core host serves both the client and the API.
- **No — a native client, a MAUI app, another service, a third-party integration** → pattern 3. Use
  the default bearer transport. There is no cookie jar worth relying on, and the client must hold the
  token.

Both work in the same server process. A presented `Authorization` header always wins over a cookie,
so a MAUI client and a browser client can talk to one backend without either being aware of the
other.

> **What the cookie actually buys you.** An `HttpOnly` cookie stops a script from *stealing* the token
> and replaying it later or elsewhere. It does **not** stop that script from making authenticated
> requests from the victim's browser while the session lasts. The gain is against exfiltration and
> persistence, not against abuse in the moment. Do not treat it as a reason to relax about XSS.

---

## Pattern 3 — bearer tokens (the default)

Nothing to configure. `AddBlazorBaseUserServer` registers `IdentityBearerAuthTransport` over ASP.NET
Core Identity's own bearer-token scheme (`AddBearerToken`, scheme name
`IdentityConstants.BearerScheme` unless renamed). The token pair comes back in the response body, and
the client stores it and sends `Authorization: Bearer …`.

```csharp
builder.Services.AddBlazorBaseUserServer<AppUser, AppDbContext>(builder.Configuration);
// ...
app.MapBlazorBaseAuthSessionEndpoint();
```

If the client is a browser, leave `AddBlazorBaseUserWasm`'s token storage at its default —
`InMemoryTokenStorage` — rather than opting into `localStorage`. Anything in browser storage survives
a reload and is readable by any script that gets injected into the page.

The access token is opaque and data-protected, so a Blazor client cannot decode its own claims from it
any more than it could from a cookie — it has to ask the server via `GET /api/auth/session`, same as
under the cookie transport (see below). `AddBlazorBaseUserServer` therefore requires that endpoint to
be mapped by default and fails at startup otherwise. A pure Web API with no BlazorBase client — served
only to native or service clients that hold the bearer token themselves — states that explicitly:

```csharp
builder.Services.AddBlazorBaseUserServer<AppUser, AppDbContext>(builder.Configuration, options =>
{
    options.RequiresAuthSessionEndpoint = false;
});
```

---

## Pattern 1 — cookies (backend-for-frontend)

```csharp
builder.Services.AddBlazorBaseUserServer<AppUser, AppDbContext>(builder.Configuration);
builder.Services.AddBlazorBaseCookieAuthTokens(builder.Configuration);
```

That single extra call changes three things:

1. Token delivery switches to `IdentityCookieAuthTransport`, which signs the caller into ASP.NET Core
   Identity's own application cookie (`IdentityConstants.ApplicationScheme`) instead of handing back a
   token pair, and blanks `AccessToken`/`RefreshToken` out of the response body. The body still
   carries `ExpiresAt` so a client can schedule its next session check. The cookie handler brings
   sliding expiration, `SecurityStampValidator` re-checks and automatic chunking of an oversized cookie
   along with it — none of which a hand-rolled cookie would have had.
2. The authentication default becomes a policy scheme that forwards a bearer request to the JWT
   handler and a cookie request to Identity's application-cookie handler, so the same endpoints serve
   both kinds of client without either being aware of the other.
3. The refresh and logout endpoints act on the cookie alone and ignore any token in the request body —
   honouring a body value would let a caller present a token the browser never held. There is no
   separate refresh token under this transport: the session lives entirely in the Identity cookie,
   renewed by sliding expiration on every request.

### If you renamed the bearer scheme

`AddBlazorBaseUserServer` lets a host choose the scheme name via
`BlazorBaseUserServerOptions.SchemeName`. `AddBlazorBaseCookieAuthTokens` reads that choice back out
of the already-registered options instance, so it normally needs no help — but a host that also wants
to name the fallback scheme explicitly can still pass it:

```csharp
builder.Services.AddBlazorBaseUserServer<AppUser, AppDbContext>(builder.Configuration, options =>
{
    options.SchemeName         = "AppJwt";
    options.ClaimDefaultScheme = false;
});
builder.Services.AddBlazorBaseCookieAuthTokens(builder.Configuration, bearerSchemeName: "AppJwt");
```

### Registration order does not matter

`AddBlazorBaseCookieAuthTokens` replaces the transport rather than adding one, so it wins regardless
of whether it runs before or after `AddBlazorBaseUserServer`.

### Configuration

Bound from the `AuthCookies` section, overridable in code.

| Key | Default | Notes |
|---|---|---|
| `CookieName` | `__Host-bb_session` (or `bb_session` when `SecureOnly` is off) | |
| `CsrfHeaderName` | `X-CSRF-TOKEN` | Header the antiforgery request token is sent in |
| `SecureOnly` | `true` | Only turn off for plain-HTTP local development |
| `SameSite` | `Lax` | See below — `Strict` breaks OIDC sign-in |
| `ExpireTimeSpan` | the framework's refresh-token window | Sliding expiration; renewed on every request |
| `AllowSameSiteRequests` | `false` | See [split-origin deployment](#split-origin-appapi-deployment) below |

> **Do not set `SameSite=Strict`.** An OIDC provider returns the user with a cross-site `form_post`,
> and a strict cookie is not sent on that request. The user lands back on your application logged
> out, and nothing in the logs says why.

---

## CSRF — mandatory once you use cookies

A cookie is attached by the browser automatically, so a request forged on another site arrives fully
authenticated. Bearer tokens do not have this problem; cookies bring it back, and it is your job to
close it.

```csharp
app.UseAuthentication();
app.UseBlazorBaseCsrf();
app.UseAuthorization();
```

`UseBlazorBaseCsrf()` enforces `BlazorBaseCsrfMiddleware` for every state-changing request from that
point in the pipeline onward — including MVC controller actions, which a per-endpoint filter could
never reach. It must run **after** `app.UseAuthentication()`: the antiforgery token is bound to the
caller's identity, so it can only be validated once that identity has been resolved. Calling
`AddBlazorBaseCookieAuthTokens` without ever calling `UseBlazorBaseCsrf()` fails at startup rather than
silently shipping an unprotected cookie transport — see `BlazorBaseCsrfStartupGuardFilter`.

An endpoint that genuinely needs to opt out (an anonymous webhook, for example) calls
`.AllowBlazorBaseCsrf()` on its route registration. Apply this to nothing by default.

### What the middleware checks

1. **`Sec-Fetch-Site`** must be `same-origin` or `none` (or `same-site` when
   `AuthCookieOptions.AllowSameSiteRequests` is explicitly turned on, see below). The browser sets this
   and page script cannot forge it. It also covers the gap `SameSite` leaves: "same site" is
   registrable-domain scoped, so a hostile or compromised sibling subdomain counts as same-site and its
   requests carry your cookie. It is absent on older clients, which is why it is not the only check.
2. **ASP.NET Core's `IAntiforgery`.** The request token is data-protected and **bound to the caller's
   identity**, so one minted for somebody else does not validate.

> A plain random value echoed between a readable cookie and a header — the classic "double submit" —
> looks equivalent and is not. Anything able to write a cookie for your domain, a sibling subdomain
> included, can plant both halves of that pair and satisfy the check. An identity-bound token cannot
> be forged that way. This is why the framework uses the platform's own antiforgery service rather
> than a hand-rolled one.

The client gets the token from `GET /api/auth/session`, in the **body**. There is deliberately no
readable cookie to read it from.

The two auth endpoints that act on a cookie alone — `POST /api/auth/refresh` and
`POST /api/auth/logout` — are covered the same way as every other endpoint, because
`BlazorBaseCsrfMiddleware` runs for the whole pipeline rather than being attached per-endpoint. The
cookie transport additionally answers `IAuthTokenTransport.IsRequestIntentionalAsync` through the same
checks, so a cross-site page cannot rotate or revoke a visitor's session. The bearer transport answers
`true` there unconditionally: a bearer token has to be attached deliberately, which is the proof of
intent a cookie cannot give.

Safe methods (`GET`, `HEAD`, `OPTIONS`, `TRACE`) pass through untouched.

> **This makes `GET` your responsibility.** The middleware cannot tell a reading `GET` from a writing
> one. A `GET /api/orders/{id}/cancel` is reachable cross-site whatever else you configure. Keep `GET`
> free of side effects.

### Split-origin `app.`/`api.` deployment

A deployment that serves the client at `app.example.com` and the API at `api.example.com` — two
subdomains of one registrable domain — is "same-site" but not "same-origin". `Sec-Fetch-Site` reads
`same-site` there, which the default posture above rejects. Set
`AuthCookies:AllowSameSiteRequests = true` to accept it once that split-origin deployment is the one
actually in use; doing so relies on the antiforgery token check alone to stop a cross-site request,
since `Sec-Fetch-Site` no longer distinguishes the two. See
[BlazorBase.User.Wasm](BlazorBase.User.Wasm.md#split-origin-deployment-with-the-cookie-transport) for
the client-side half of this deployment shape.

### Client side

Use the cookie variant of the WASM registration instead of the bearer one:

```csharp
builder.Services.AddBlazorBaseUserClient();
builder.Services.AddBlazorBaseUserWasmCookieAuth(builder.HostEnvironment.BaseAddress);

builder.Services
    .AddHttpClient<IAuthService, AuthService>(c => c.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<CsrfHeaderHandler>();
```

That swaps three things for you:

- `ITokenStorage` becomes `NoOpTokenStorage` — there is no token to hold.
- The authentication state comes from asking the server `GET /api/auth/session` instead of decoding a
  token it cannot read.
- `CsrfHeaderHandler` attaches the antiforgery token from `CsrfTokenProvider` to every write and sets
  `BrowserRequestCredentials.Include` so the browser sends the cookies.

The antiforgery token is bound to the identity of the request that minted it, so the client must
re-read the session after signing in.

Register the handler on **every** typed client that talks to the API. The framework cannot know which
of a host's clients those are, and one that is missed rejects every write with a 403.

**Do not also register `AuthTokenHandler`.** It exists to attach and refresh a token the client holds;
under this transport there is none. It would attach nothing and refresh against a body the server
ignores.

On the server, map the session endpoint alongside the rest:

```csharp
app.MapBlazorBaseAuthSessionEndpoint();
```

Calling `AddBlazorBaseCookieAuthTokens` without ever mapping this endpoint fails at startup rather
than silently shipping a client that can never read its own claims or obtain an antiforgery token —
see `BlazorBaseAuthSessionEndpointStartupGuardFilter`. That check applies regardless of
`BlazorBaseUserServerOptions.RequiresAuthSessionEndpoint`: the cookie transport's own registration is
unconditional, so a host that opted out and then adopted cookies has contradicted itself and is still
caught.

It echoes the caller's own claims and answers an anonymous caller with `IsAuthenticated: false`
rather than a challenge — a client has to be able to ask "am I signed in?" without being redirected
for asking. It grants nothing: the server re-checks every request regardless of what the client
believes about itself.

> Use the session endpoint rather than `GET /api/user/me` for this. `UserProfile` carries a single
> `Role` string, so a user with several roles would lose all but one and every role-gated piece of UI
> would vanish.

### Do not undo it with CORS

`AllowAnyOrigin` together with `AllowCredentials`, or a reflected origin, defeats the header check. A
hosted WebAssembly application is same-origin and needs no CORS policy at all — the safest
configuration is not to add one.

---

## Behind a reverse proxy — forwarded headers

Register `AddBlazorBaseForwardedHeaders(configuration)` before `app.UseForwardedHeaders()` whenever
the host sits behind a reverse proxy or load balancer:

```csharp
builder.Services.AddBlazorBaseForwardedHeaders(builder.Configuration);
// ...
app.UseForwardedHeaders();
```

```json
{
  "ForwardedHeaders": {
    "KnownProxies": ["10.0.0.4"],
    "KnownNetworks": ["10.0.0.0/24"]
  }
}
```

This matters because the framework's `"BlazorBaseAuth"` rate-limiter policy and the brute-force delay
service both key on `HttpContext.Connection.RemoteIpAddress` — which, behind a proxy, is the proxy's
own address, identical for every client, until forwarded headers are processed. Without this call
every client is rate-limited as if it were one caller, and a single abusive client can exhaust the
shared bucket for everyone behind the same proxy.

The registration deliberately does **not** fall back to an unrestricted trust list when
`KnownProxies`/`KnownNetworks` is left empty: an unrestricted trust list would let any caller supply
its own `X-Forwarded-For` value and pick its own rate-limit partition, handing the attacker — not the
network topology — the choice of key. With nothing configured, forwarded headers middleware still
runs but forwards nothing, so `RemoteIpAddress` stays exactly what it was before. A host that forgets
to call this at all behind a real proxy is warned at startup rather than left silently partitioning
every client into the same bucket.

---

## Key management — the key ring is your session store

Whichever transport you pick, the thing that actually holds your sessions together is the ASP.NET
Core Data Protection key ring. This framework signs nothing of its own: the bearer access and refresh
tokens, the application cookie, the antiforgery token and the two-factor pending ticket are all
protected through it. There is no secret in configuration to get wrong, and no secret to leak — but
the key ring is now an authentication concern rather than a background detail.

Two failure modes follow, and neither announces itself:

- **Keys that do not survive a restart sign everyone out.** ASP.NET Core's default key location is
  the local machine or user profile. A container's filesystem is neither: it is discarded when the
  container is replaced, which is what a redeploy does, so every deployment logs out every user.
  The keys have to be written somewhere that outlives the container — a named volume or a bind mount
  that `PersistKeysToFileSystem` points at, or a store outside the filesystem entirely such as the
  application's database or blob storage.

  Watch the difference between *restart* and *replace*. An anonymous volume survives
  `docker restart` and disappears with `docker compose down`; a Kubernetes `emptyDir` is gone the
  moment the pod is rescheduled. Neither is persistence in the sense that matters here. Use a named
  volume, a bind mount, or a `PersistentVolumeClaim`, and check that it is still there after a
  deployment rather than after a restart.
- **Keys that are not shared make instances reject each other.** Two instances with their own key
  rings cannot read each other's cookies or tokens. This does not fail cleanly — it looks like
  sporadic 401s that follow the load balancer rather than the user.

So decide key management deliberately, at the same time as the transport:

| Situation | What to do |
|---|---|
| Local development, single machine | Nothing. The default is fine. |
| One instance, durable and backed-up storage | `PersistKeysToFileSystem(…)` plus `SetApplicationName(…)` |
| Several instances, or keys must be encrypted at rest | [BlazorBase.DataProtection.AzureKeyVault](BlazorBase.DataProtection.AzureKeyVault.md) |

For a single container, that is a named volume and a matching registration — both halves are
required, and neither is any use alone:

```yaml
services:
  app:
    volumes:
      - dataprotection-keys:/keys
volumes:
  dataprotection-keys:
```

```csharp
builder.Services.AddBlazorBaseDataProtection(dataProtection => dataProtection
    .PersistKeysToFileSystem(new DirectoryInfo("/keys"))
    .SetApplicationName("MyApp"));
```

The keys are unencrypted in that volume, so treat it as a secret: back it up with the application,
and keep it out of an image layer or a bind mount that lands in source control.

`SetApplicationName` must be stable and identical on every instance in all of the last two rows —
left unset it is derived from the content-root path, so two instances deployed to different paths
silently do not share keys even when they share a store.

Note that a `ProtectKeysWith*` call only **encrypts** the key ring; it does not store it. Encryption
at rest never replaces a `PersistKeysTo*` call. See
[BlazorBase.DataProtection.md](BlazorBase.DataProtection.md) for the base library and
[BlazorBase.DataProtection.AzureKeyVault.md](BlazorBase.DataProtection.AzureKeyVault.md) for the
Azure option, including what it costs — a start-up dependency on Key Vault, and a vault key whose
loss is permanent.

---

## External sign-in: two entry points, one resolver

Both paths resolve the user through the same `IExternalUserResolver<TUser>` and mint through the same
`AuthTokenIssuer<TUser>`. Only the way the external identity arrives differs.

### Redirect (for pattern 1)

The framework does **not** own the OIDC protocol. Register your own handler and keep everything it
gives you — including `ITokenAcquisition` for calling a downstream API on the user's behalf, which
only works while those tokens stay on the server.

```csharp
builder.Services
    .AddAuthentication()
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
        .AddMicrosoftGraph(builder.Configuration.GetSection("DownstreamApi"))
        .AddInMemoryTokenCaches();

builder.Services.AddScoped<IExternalUserResolver<AppUser>, AppExternalUserResolver>();

app.MapBlazorBaseExternalAuthRedirectEndpoints<AppUser>(
    externalScheme: OpenIdConnectDefaults.AuthenticationScheme);
```

This maps:

- `GET /api/auth/external/challenge?returnUrl=…` — starts the provider's flow
- `GET /api/auth/external/complete?returnUrl=…` — converts the provider's session into a framework
  session, writes it through the configured transport, signs out of the provider's own session, and
  redirects

`returnUrl` is validated as a local path. Protocol-relative values (`//host`, `/\host`) are rejected —
a redirect immediately after a successful sign-in is the most convincing phishing hop there is.

> `AddInMemoryTokenCaches` loses the cache on restart and is not shared between instances. Fine for a
> single instance; when you scale out, half the requests start asking for consent again. Use a
> distributed cache in that case.

### Token exchange (for pattern 3)

The client obtains the provider token itself and posts it:

```csharp
builder.Services.AddBlazorBaseExternalAuth(builder.Configuration);
app.MapBlazorBaseExternalAuthEndpoints<AppUser>();
```

`POST /api/auth/external/exchange` with `{ "externalToken": "…" }`.

The external token itself is validated by `OidcExternalTokenValidator` against the configured
authority's published signing keys — issuer validation is never optional, and the framework does not
expose a way to turn it off. For a multi-tenant authority (`/common`, `/organizations`, `/consumers`)
the token's tenant is additionally checked against `IExternalTenantAllowList`; the default
implementation answers from a static `ExternalAuthSettings.AllowedTenantIds` list, and a host whose
acceptable tenants are rows in its own database registers its own implementation instead. See
`BlazorBase.User.Server`'s XML documentation on `ExternalAuthSettings`,
`ExternalAuthSettingsValidator` and `IExternalTenantAllowList` for the full contract.

### Writing the resolver

Rejection is a return value, not an exception:

```csharp
public sealed class AppExternalUserResolver(AppDbContext db) : IExternalUserResolver<AppUser>
{
    public async Task<ExternalUserResolution<AppUser>> ResolveAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var subject = principal.FindFirst("oid")?.Value;

        if (subject is null)
            return ExternalUserResolution<AppUser>.Reject("No subject claim.");

        var user = await db.Users.SingleOrDefaultAsync(u => u.ExternalId == subject, ct);

        return user is null
            ? ExternalUserResolution<AppUser>.Reject("Unknown subject.")
            : ExternalUserResolution<AppUser>.Accept(user);
    }
}
```

Two things to get right here, both of which have gone wrong in real applications:

**Key on the subject id, not the email address.** An email in a directory is mutable and
*reassignable*. When someone leaves and their address is later given to a new hire, keying on email
hands the new person the old account together with its roles. The `oid` claim is stable and never
reused. Use the email only once, to link an invited user on first sign-in, and never again.

**Decide explicitly whether an unknown subject may provision itself.** With a single-tenant authority
that is bounded to your own directory and is often fine. With a multi-tenant authority (`/common`) it
means every account in existence can create itself an account in your application. If you have an
invitation flow, an unknown subject should be a failed sign-in.

---

## Writing your own transport

Implement `IAuthTokenTransport` and register it. If your `WriteAsync` puts the tokens somewhere the
client cannot read, **blank them in the returned value** — returning them in the body as well hands
them straight back to the script the transport exists to keep them from.

## Checklist for a browser application

- [ ] `AddBlazorBaseCookieAuthTokens` called, with the right bearer scheme name if you renamed it
- [ ] `MapBlazorBaseAuthSessionEndpoint()` mapped
- [ ] `AddBlazorBaseUserWasmCookieAuth` used instead of `AddBlazorBaseUserWasm`
- [ ] `CsrfHeaderHandler` added to every typed client that talks to the API
- [ ] `AuthTokenHandler` **not** registered
- [ ] `app.UseBlazorBaseCsrf()` called, after `app.UseAuthentication()` and before `app.UseAuthorization()`
- [ ] `GET` endpoints have no side effects
- [ ] `SecureOnly` left on outside local development
- [ ] `SameSite` left at `Lax`
- [ ] The CSRF header name matches on both sides
- [ ] `AllowSameSiteRequests` only turned on for an actual split-origin `app.`/`api.` deployment
- [ ] No permissive CORS policy
- [ ] `AddBlazorBaseForwardedHeaders` called when the host sits behind a reverse proxy
- [ ] The external resolver keys on the subject id, not the email
- [ ] The external resolver's answer for an unknown subject is a deliberate decision
- [ ] Key management is decided, not defaulted — the keys survive a restart and every instance shares them

## What is not in the framework yet

- A MAUI counterpart to `AddBlazorBaseUserWasmCookieAuth`. There is deliberately none: a native client
  cannot use a browser cookie session, so pattern 1 does not apply to it. MAUI stays on the bearer
  path, which is the correct answer rather than a gap.

