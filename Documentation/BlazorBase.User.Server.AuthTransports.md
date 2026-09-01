# Authentication token transports — choosing between cookies and bearer tokens

`BlazorBase.User.Server` issues the same token pair no matter which client asks for it. What differs
is **how that pair reaches the client**, and that choice is the application's, not the framework's.

This document is the decision guide and the wiring reference. Read it before adding authentication to
a new application, and before changing it in an existing one.

---

## The three patterns, and which one you are in

The IETF's guidance for browser-based applications describes three shapes. They differ in one thing —
how much a successful cross-site-scripting attack walks away with.

| | Refresh token in browser | Access token in browser | Revocation | CSRF risk |
|---|---|---|---|---|
| **1. Backend-for-frontend** | no | no | immediate | yes, must be handled |
| **2. Token-mediating backend** | no | yes, short-lived | at expiry | no |
| **3. Browser as OAuth client** | yes | yes | at expiry | no |

**Pick by asking one question: does a backend you control serve the client and own the API?**

- **Yes, same origin** → pattern 1. Use `AddBlazorBaseCookieAuthTokens`. This is the right default for
  a hosted Blazor WebAssembly application, where the ASP.NET Core host serves both the client and the
  API.
- **No — a native client, a MAUI app, another service, a third-party integration** → pattern 3. Use
  the default body transport. There is no cookie jar worth relying on, and the client must hold the
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

Nothing to configure. `AddBlazorBaseUserServer` registers `BodyAuthTokenTransport`, the token pair
comes back in the response body, and the client stores it and sends `Authorization: Bearer …`.

```csharp
builder.Services.AddBlazorBaseUserServer<AppUser, AppDbContext>(builder.Configuration);
```

If the client is a browser, store the token **in memory**, never in `localStorage` or
`sessionStorage`. Anything in browser storage survives a reload and is readable by any script that
gets injected into the page.

---

## Pattern 1 — cookies (backend-for-frontend)

```csharp
builder.Services.AddBlazorBaseUserServer<AppUser, AppDbContext>(builder.Configuration);
builder.Services.AddBlazorBaseCookieAuthTokens(builder.Configuration);
```

That single extra call changes three things:

1. The token pair is written to `HttpOnly` cookies and **blanked out of the response body**. The body
   still carries `ExpiresAt` so a client can schedule a refresh.
2. The JWT handler learns to read the access token from its cookie — but only when no
   `Authorization` header was presented.
3. The refresh endpoint reads the refresh token from its cookie and **ignores the request body**.
   Honouring a body value would let a caller present a token the browser never held.

### If you renamed the JWT scheme

`AddBlazorBaseUserServer` lets a host choose the scheme name. If you used that, pass the same name
here, or the cookie fallback is attached to a handler nobody uses and every request comes back
unauthenticated with nothing in the log to explain it:

```csharp
builder.Services.AddBlazorBaseUserServer<AppUser, AppDbContext>(builder.Configuration, options =>
{
    options.SchemeName         = "AppJwt";
    options.ClaimDefaultScheme = false;
});
builder.Services.AddBlazorBaseCookieAuthTokens(builder.Configuration, schemeName: "AppJwt");
```

### Registration order does not matter

`AddBlazorBaseCookieAuthTokens` replaces the transport rather than adding one, so it wins regardless
of whether it runs before or after `AddBlazorBaseUserServer`.

### Configuration

Bound from the `AuthCookies` section, overridable in code.

| Key | Default | Notes |
|---|---|---|
| `AccessTokenCookieName` | `bb_access` | |
| `RefreshTokenCookieName` | `bb_refresh` | |
| `CsrfCookieName` | `XSRF-TOKEN` | Deliberately readable by script |
| `CsrfHeaderName` | `X-CSRF-TOKEN` | |
| `RefreshCookiePath` | `/api/auth` | Must match where the auth endpoints are mapped |
| `SecureOnly` | `true` | Only turn off for plain-HTTP local development |
| `SameSite` | `Lax` | See below — `Strict` breaks OIDC sign-in |
| `RefreshCookieDays` | falls back to `JwtSettings:RefreshExpirationDays` | |

> **Do not set `SameSite=Strict`.** An OIDC provider returns the user with a cross-site `form_post`,
> and a strict cookie is not sent on that request. The user lands back on your application logged
> out, and nothing in the logs says why.

---

## CSRF — mandatory once you use cookies

A cookie is attached by the browser automatically, so a request forged on another site arrives fully
authenticated. Bearer tokens do not have this problem; cookies bring it back, and it is your job to
close it.

```csharp
app.MapPost("/api/orders", CreateOrder).RequireBlazorBaseCsrf();

// or for a whole group
var group = app.MapGroup("/api/orders").RequireBlazorBaseCsrf();
```

The filter is **not** applied automatically. A bearer-only application does not need it, and forcing
a header on one would break it.

### What the filter checks

1. **`Sec-Fetch-Site`** must be `same-origin` or `none`. The browser sets this and page script cannot
   forge it. It also covers the gap `SameSite` leaves: "same site" is registrable-domain scoped, so a
   hostile or compromised sibling subdomain counts as same-site and its requests carry your cookie.
2. **Double submit.** The readable `XSRF-TOKEN` cookie must match the `X-CSRF-TOKEN` header. A
   cross-site attacker can make the browser send cookies but cannot set a custom header without a
   CORS preflight, which a correctly configured server refuses.

Safe methods (`GET`, `HEAD`, `OPTIONS`, `TRACE`) pass through untouched.

> **This makes `GET` your responsibility.** The filter cannot tell a reading `GET` from a writing one.
> A `GET /api/orders/{id}/cancel` is reachable cross-site whatever else you configure. Keep `GET`
> free of side effects.

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
- The authentication state comes from `CookieAuthStateProvider`, which asks the server
  `GET /api/auth/session` instead of decoding a token it cannot read. It stays the same concrete
  type the UI components depend on, so nothing else has to change.
- `CsrfHeaderHandler` attaches the CSRF header to every write and sets
  `BrowserRequestCredentials.Include` so the browser sends the cookies.

Register the handler on **every** typed client that talks to the API. The framework cannot know which
of a host's clients those are, and one that is missed rejects every write with a 403.

**Do not also register `AuthTokenHandler`.** It exists to attach and refresh a token the client holds;
under this transport there is none. It would attach nothing and refresh against a body the server
ignores.

On the server, map the session endpoint alongside the rest:

```csharp
app.MapBlazorBaseAuthSessionEndpoint();
```

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

## External sign-in: two entry points, one resolver

Both paths resolve the user through the same `IExternalUserResolver<TUser>` and mint through the same
`TokenService`. Only the way the external identity arrives differs.

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

- [ ] `AddBlazorBaseCookieAuthTokens` called, with the right scheme name if you renamed it
- [ ] `MapBlazorBaseAuthSessionEndpoint()` mapped
- [ ] `AddBlazorBaseUserWasmCookieAuth` used instead of `AddBlazorBaseUserWasm`
- [ ] `CsrfHeaderHandler` added to every typed client that talks to the API
- [ ] `AuthTokenHandler` **not** registered
- [ ] `RequireBlazorBaseCsrf()` on every state-changing endpoint
- [ ] `GET` endpoints have no side effects
- [ ] `SecureOnly` left on outside local development
- [ ] `SameSite` left at `Lax`
- [ ] `RefreshCookiePath` matches where the auth endpoints are mapped
- [ ] The CSRF cookie and header names match on both sides
- [ ] No permissive CORS policy
- [ ] The external resolver keys on the subject id, not the email
- [ ] The external resolver's answer for an unknown subject is a deliberate decision

## What is not in the framework yet

- A MAUI counterpart to `AddBlazorBaseUserWasmCookieAuth`. There is deliberately none: a native client
  cannot use a browser cookie session, so pattern 1 does not apply to it. MAUI stays on the bearer
  path, which is the correct answer rather than a gap.
