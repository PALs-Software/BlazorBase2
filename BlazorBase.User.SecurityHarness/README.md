# BlazorBase.User.SecurityHarness

An offensive security test harness for the BlazorBase2 authentication stack
(`BlazorBase.User.Server`). It starts real Kestrel hosts - not `TestServer` - for every
authentication configuration the framework supports, and runs a catalogue of adversarial probes
against them.

**This is an attack tool. Only point it at hosts you own and are authorised to test.** Every host
it starts is its own, in-process, loopback-only Kestrel instance; it never talks to the real
Microsoft identity platform or any production system unless `--base-url` is pointed at one on
purpose.

The critical review written from a full run of this catalogue lives in
[`Libs/Documentation/BlazorBase.User.SecurityReview.md`](../Documentation/BlazorBase.User.SecurityReview.md).

## What this step adds

The runnable skeleton: a profile-based host launcher, the probe abstraction, and three reporters.
At the end of this step the tool runs, starts every host profile, discovers zero probes, and prints
a valid, empty report. No probe is implemented here - later steps only need to add a file under
`Probes/` and it is picked up automatically.

## Core session probes (`Probes/CoreSession/`)

23 probes against the `Bearer` profile: password login (wrong password, unknown user, inactive
account, lockout and its reset, a timing-side-channel measurement, password policy, case
sensitivity), rate limiting (permit-limit trigger, `X-Forwarded-For` spoofing, the shared-partition
trade-off, window recovery), and the token lifecycle (expiry, refresh rotation, replay and family
revocation, concurrent refresh, logout and its idempotency, security-stamp invalidation latency,
cross-user refresh, cross-transport token acceptance, and malformed/oversized token values).

Every one of these probes starts its own private instance of the `Bearer` profile through
`ProbeContext.StartIsolatedHostAsync` - its own port, its own SQLite file - rather than sharing the
one host the main run started for the profile. A probe that needs a shortened access-token
lifetime, a lowered rate-limit permit count, or a shortened lockout window asks for it through
`AuthTokens:*` configuration overrides on that private host; no probe's rate-limit or lockout state
can leak into another's, and a probe that measures latency is never skewed by another probe's
requests sharing the same budget.

Two probes had to establish, empirically, whether a mechanism honours the harness's
DI-registered `TimeProvider` before deciding whether to advance the clock or fall back to a real,
short wait:

- **`RATE-WINDOW-RECOVERY`**: the fixed-window rate limiter does **not** honour it - it was built
  with a plain `FixedWindowRateLimiterOptions` that never has its `TimeProvider` property assigned,
  so it defaults to the real wall clock.
- **`LOGIN-LOCKOUT-RESETS`**: ASP.NET Core Identity's lockout does **not** honour it either -
  `UserManager.AccessFailedAsync` computes the lockout end from `DateTimeOffset.UtcNow` directly,
  not from any injected clock.

Both probes therefore advance the harness clock first (to demonstrate the non-effect), then fall
back to a short, `AuthTokens:LockoutSeconds` / `AuthTokens:RateLimit:WindowSeconds`-configured real
wait to observe recovery - kept to a few seconds so the run stays fast.

`TOKEN-EXPIRY` and `TOKEN-STAMP-INVALIDATION` needed an actual authenticated route to observe a
`401`/`200` distinction on, which no profile mapped: `HarnessHostFactory` now also maps
`GET harness/protected-probe` behind `RequireAuthorization()` - the harness's own minimal route, not
a framework endpoint - purely so a probe can tell whether a given access token still authenticates.

## Cookie transport and CSRF probes (`Probes/CookieTransport/`)

19 probes against the `Cookie` profile (cookie attributes, CSRF, transport confusion) plus one
(`CSRF-GUARD-FIRES`) against `CookieWithoutCsrfMiddleware`. Every one of these probes also starts
its own private instance of its profile through `ProbeContext.StartIsolatedHostAsync`, for the same
reason the core-session probes do: `api/auth/login` is rate-limited, and a cookie/CSRF probe logs in
at least once, so sharing one host's login budget across every probe in the category would starve
whichever probes happened to run later.

`CookieTransportSupport` is the shared plumbing: it reads the session cookie's configured name and
the antiforgery header name back from the host's own `AuthCookieOptions` rather than hardcoding a
default, fetches the antiforgery token an anonymous caller needs before `api/auth/login` will even
accept a request, and logs in with that token attached. `api/auth/login` carries no exemption from
`BlazorBaseCsrfMiddleware` of its own, so this anonymous-token-then-login sequence is a precondition
for every probe here, not just the ones that are explicitly about CSRF.

Cookie-attribute probes (`COOKIE-HTTPONLY`, `COOKIE-SECURE`, `COOKIE-SAMESITE`, `COOKIE-HOST-PREFIX`)
parse the raw `Set-Cookie` header text directly, rather than reading it back through the HTTP
client's own cookie container - the container normalises attributes away, which would make a probe
assert on .NET's parsing of the header instead of on the bytes the server actually sent.

`HarnessHostFactory` maps two more harness-only endpoints for these probes to target:
`POST harness/guarded-probe` (no authorization requirement) and `POST harness/protected-guarded-probe`
(`RequireAuthorization()`) - two state-changing routes shaped like a real application endpoint, with
no `[AllowBlazorBaseCsrf]` exemption of their own, so `BlazorBaseCsrfMiddleware` is the only thing
standing between a forged request and either one. `CSRF-EXEMPT-ENDPOINTS` enumerates every endpoint
the running host actually mapped through `EndpointDataSource`, by reflection over each endpoint's
metadata for `AllowBlazorBaseCsrfAttribute` - never by grepping source, since an exemption is a
property of what a host maps, not of what the framework merely offers a way to mark.

`CSRF-GUARD-FIRES` targets `CookieWithoutCsrfMiddleware`, whose host never serves a request
successfully at all - `BlazorBaseCsrfStartupGuardFilter` throws on the very first one, which is why
that profile is reported `FailedToStart` rather than `Started`. `ProbeContext` now carries an
optional `StartupGuardFailure` string, and `Program.cs` builds a context for every profile regardless
of whether its host started - a normal, live one when it did, or a degenerate one exposing only that
failure message when it did not - so this probe still gets a `ProbeContext` to read a verdict from,
without any of its other members (`BaseAddress`, `HttpClientFactory`, and so on) ever being called.

## External token exchange probes (`Probes/ExternalExchange/`)

24 probes against the `External` profile, attacking `POST api/auth/external/exchange` and the fake
identity provider from step 2: signature and algorithm confusion, claim validation (audience,
issuer, `tid` allow-listing including a casing/whitespace check, required scope), lifetime (expiry,
the 30-second clock skew, not-yet-valid, missing `exp`), replay, an oversized token, a rate-limit
flood, provider key rotation in both `BaseConfigurationManager` refresh modes, provider availability,
and the two account-linking probes the harness's dual resolver switch exists for.

`EXT-VALID` is the canary every other result in the category depends on. Every probe seeds a local
user whose username is the token's own subject before exchanging it - `ExternalUserResolverSwitch`
defaults to `StrictSubjectExternalUserResolver`, so without a matching local account, an *accepted*
signature would still come back `401` for the wrong reason (no such user), silently hiding a real
validator bypass behind an unrelated rejection. `ExternalExchangeSupport.EnsureResolvableSubjectAsync`
is the one-line precondition every probe here calls for exactly that reason.

Four probes report numbers rather than a verdict, per `ProbeResult.Inconclusive`:
`EXT-EXPIRED-WITHIN-SKEW` (how long past `exp` a token stays usable), `EXT-REPLAY` (how many
distinct local sessions one provider token can mint, bounded to five attempts), `EXT-KEY-ROTATION`
(how many requests a rotated key takes to be accepted, measured once in the library's default
fire-and-forget refresh mode and once with `PinnedThumbprintConfigurationManagerFactory.SetBlockingRefreshMode(true)`,
each on its own isolated host so neither measurement starts from a warm `BaseConfigurationManager`
cache), and `EXT-OFFLINE-THEN-ONLINE` (whether a request against a cold cache while the provider is
down leaves any lock-out behind once it recovers). `EXT-PROVIDER-OFFLINE` is a related but
pass/fail probe: it primes the cache with a successful exchange first, so the offline request that
follows is testing cache survival specifically, not a cold start.

Probes that rotate keys or flip the provider's availability restore that shared state in a
`finally` block rather than starting a second identity-provider process - the fake provider is one
Kestrel host, started once in `Program.cs` and shared by every isolated app host the `External`
profile starts, so its `FakeSigningKeyRegistry` and online/offline switch are process-wide for the
whole run. `HarnessHostOptions.IdentityProviderHost` and a singleton registration in
`HarnessHostFactory.RegisterExternalAuth` are what let a probe reach that shared instance through
`context.Services` instead of through `Program.cs` directly.

The two account-linking probes (`EXT-UNVERIFIED-EMAIL-TAKEOVER`, `EXT-STRICT-RESOLVER-REJECTS`) send
the same token - an unverified `email` claim matching an existing local account, from a subject no
local account carries - against the naive and the strict resolver in turn. `HarnessHostFactory` maps
one more harness-only endpoint for these two probes, `GET harness/whoami`, so a probe can confirm
*whose* account a set of minted tokens actually belongs to instead of trusting a bare `200`.

Every rejection probe attaches the token it sent - header and payload decoded back from base64url,
never left opaque - alongside the response, as `ProbeEvidence`; `Program.cs`'s `FormatEvidence` folds
that request/response pair into the rendered report next to each probe's one-line description, so a
finding is legible directly from the report file without a debugger.

## Redirect flow, access tokens and two-factor probes (`Probes/RedirectFlow/`, `Probes/AccessTokens/`, `Probes/TwoFactor/`)

25 probes: 6 against a real browser-redirect OIDC flow on the `External` profile, 10 against
`bbat_`-prefixed machine access tokens on the `AccessTokens` profile, and 9 against the two-factor
endpoints on the `Bearer` profile.

**The redirect flow runs against a real `AddOpenIdConnect` handler**, not a stub.
`HarnessHostFactory.RegisterRedirectFlowAuthentication` registers `AddCookie` (the sign-in scheme)
and `AddOpenIdConnect` additively on the `External` profile, pointed at the fake identity provider
over a certificate-pinned backchannel - `FakeIdentityProviderHost` now also serves a real
`oauth2/v2.0/authorize` (auto-approves every request as the fixed `RedirectFlowSubject`, mints a
single-use authorization code) and a real `oauth2/v2.0/token` endpoint (redeems the code for a freshly
RS256-signed id_token). `RedirectFlowSupport.RunChallengeAsync` drives the whole thing by manually
following every redirect a browser would - `challenge` to the provider's `authorize`, its redirect to
the OIDC handler's own callback path (`/signin-oidc`), and that callback's redirect to `complete` -
since the probe client never follows redirects automatically.

Getting this far needed two harness fixes, both empirically diagnosed rather than assumed:

- **`External` now runs over HTTPS** (`HarnessHostFactory.HttpsProfiles`), not plain HTTP as before.
  The OIDC handler's correlation cookie is `SameSite=None`, which ASP.NET Core unconditionally
  upgrades to `Secure=true` regardless of configuration - over HTTP that cookie can never come back,
  and the callback fails with `AuthenticationFailureException: Correlation failed.` every time. Real
  OIDC deployments are HTTPS-only for exactly this reason.
- A redirect-flow probe's own client must trust the fake provider's certificate directly (it visits
  the provider's HTTPS `authorize` endpoint itself, simulating the browser hop), which
  `ProbeContext.HttpClientFactory` does not provide for a plain-HTTP profile.
  `RedirectFlowSupport.CreateClient` builds one pinned to the certificate, now registered as a
  singleton in every profile's DI container by `HarnessHostFactory` for exactly this purpose.

With both fixed, the OIDC handshake itself completes correctly end to end - but
**`ExternalAuthRedirectEndpointMapper.Complete` can never succeed**, wired exactly as its own XML
documentation prescribes (`externalScheme = OpenIdConnectDefaults.AuthenticationScheme`).
`Complete`'s `httpContext.AuthenticateAsync(externalScheme)` call always fails against a genuine
`RemoteAuthenticationHandler` (OpenID Connect, OAuth): remote schemes only produce a principal during
their own callback processing, and `RemoteAuthenticationHandler<TOptions>.HandleAuthenticateAsync()`
answers every other call with `AuthenticationFailureException: Remote authentication does not support
authenticate`-shaped failure regardless of whether the handshake succeeded. The framework's own test
suite (`ExternalAuthRedirectEndpointMapperTests`) does not catch this because it substitutes a
synthetic scheme whose `HandleAuthenticateAsync` trivially returns success - nothing like a real
remote handler. `REDIR-COMPLETE-REPLAY`, `REDIR-RETURNURL-PRESERVED` and `REDIR-OPEN-REDIRECT` are
`Inconclusive` for this reason and say so in their own evidence; `REDIR-COMPLETE-WITHOUT-CHALLENGE`,
`REDIR-NO-STORE` and `REDIR-CHALLENGE-RATE-LIMIT` do not depend on a completed flow and pass.
`REDIR-OPEN-REDIRECT` is one probe over the full open-redirect/header-injection payload table, not
eleven; its evidence lists every payload and the `Location` chain it produced.

**Access tokens** (`AccessTokens` profile): probes mint and revoke tokens directly through
`IAccessTokenService` against the profile's own service provider, and call a new harness-only route,
`GET harness/access-token-probe`, gated specifically behind
`AccessTokenAuthenticationDefaults.AuthenticationScheme` (`HarnessHostFactory`) rather than the
default JWT scheme every other harness route uses. `AT-VALID` is the canary. `AT-INACTIVE-OWNER`
is a real finding: `AccessTokenService.ValidateAsync` re-checks `IsRevoked` and `ExpiresAt` against
the database on every call, but never the owning user's `IsActive` flag - a machine token keeps
authenticating after its owner is deactivated. `AT-CASE-AND-TRIM`'s whitespace half is *not* a
finding: `AccessTokenAuthenticationHandler.ExtractToken()` explicitly `.Trim()`s the bearer value
before comparison, a deliberate, harmless normalisation - the padded variant is byte-for-byte the
valid secret by the time it reaches the comparison, never a weakened check. `AT-BACKOFF` and
`AT-PREFIX-TIMING` are measurements (`Inconclusive`, numbers in the evidence), following
`LOGIN-TIMING-ORACLE`'s median/IQR approach for the latter.

**Two-factor** (`Bearer` profile): `TwoFactorSupport.EnableTwoFactorAsync` enrols an account through
the real HTTP flow - login, read the authenticator secret from `api/auth/manage/2fa`'s
`AuthenticatorUri`, compute a genuine code, and call `2fa/enable` - which needed
`HarnessHostFactory` to map `MapBlazorBaseAccountEndpoints`, never mapped by any earlier step.
`Totp` (`Probes/TwoFactor/Totp.cs`) is a from-scratch RFC 6238 implementation (base32 decode, HMAC-SHA1,
30-second step, 6 digits, no modifier) computed against the real authenticator key the framework
hands out - no package added, no framework internals reached into. `2FA-PENDING-REQUIRED` is the
canary. `2FA-PENDING-REPLAY` is a real finding: `TwoFactorPendingTokenService`'s pending token carries
no single-use tracking of its own - only its data-protection expiry - so the same pending token
completes a second login with a freshly computed code after already minting one session.
`2FA-CODE-BRUTE-FORCE` is a measurement (`Inconclusive`) run against a host with a lowered
`AuthTokens:MaxFailedAccessAttempts` override (new in `HarnessHostFactory`) so the lockout threshold
is reached in a handful of requests instead of Identity's default five.

## Account management, development login, disclosure and client pipeline probes (`Probes/AccountManagement/`, `Probes/Disclosure/`, `Probes/ClientPipeline/`)

25 probes: 10 against `api/auth/manage/*` and 5 against `api/auth/development-login` (both under
`Probes/AccountManagement/`, both on the `Bearer`/`DevelopmentLogin` profiles), 5 disclosure probes
(`Probes/Disclosure/`, split across `Bearer` and `Cookie`), and 5 against the real client-side
handler pipeline (`Probes/ClientPipeline/`, `Bearer`).

**`ACCT-RESET-INVALIDATES-SESSIONS` is the probe this group exists for.** After a password reset, a
refresh token issued beforehand is rejected immediately - `AuthControllerBase.Refresh` re-validates
the presented token's captured security stamp against the user's current one
(`AuthTokenIssuer.ValidateSecurityStampAsync`), and `UserManager.ResetPasswordAsync` rotates that
stamp as part of the reset. An access token issued beforehand does **not** reject immediately,
though - access tokens are never re-checked against the security stamp at all, the same shape as
`TOKEN-STAMP-INVALIDATION` (step 3), but demonstrated here at the exact moment a reset is meant to
matter: a password reset is what a user does after believing they are compromised, and the access
token survives it. The probe reports this as a failure rather than rounding it to "safe", and also
measures how long the access token keeps working by advancing `HarnessTimeProvider` past its natural
expiry, exactly as `TOKEN-STAMP-INVALIDATION` does.

Neither `ACCT-RESET-SINGLE-USE` nor `ACCT-CONFIRM-REUSE` special-case single-use logic that does not
exist: Identity's `DataProtectorTokenProvider` carries no "used" flag of its own, so single-use, when
it holds, is a side effect of whatever else the token's own operation changes. A password reset
rotates the security stamp the token's validation is bound to, so a reset token *is* rejected on its
second use; confirming an email does not rotate anything, so a confirmation token is *not*
rejected on its second use - `ACCT-CONFIRM-REUSE` records this as `Inconclusive`, not a hard pass or
fail, per its own doc comment: there was no a-priori reason to expect either direction.

`ACCT-RESET-EXPIRY` needed a token lifespan Identity's default (one day) makes impractical to wait
out. `AccountEndpointMapper` exposes no configuration seam for
`DataProtectionTokenProviderOptions.TokenLifespan` at all, so `HarnessHostFactory` now binds it from
`Tokens:DataProtection:TokenLifespan` - left unbound (and therefore unchanged) for every other
profile. The probe then waits a real few seconds rather than advancing `HarnessTimeProvider`, because
`DataProtectorTokenProvider` reads the real wall clock directly, never the DI `TimeProvider`.

**Development login** (`DevelopmentLogin` and `ProductionWithDevelopmentLogin` profiles, both from
step 1): `DEV-LOGIN-WORKS` is the canary. `DEV-LOGIN-ABSENT-IN-PRODUCTION` runs against the plain
`Bearer` profile rather than `ProductionWithDevelopmentLogin` on purpose - that profile fails to
start at all (`RegisterDevelopmentAuthentication` throws synchronously when the option is enabled
outside Development, the startup guard `README`'s host-profile table already documents), which is a
different and stronger claim than "the route answers 404 in a normal production deployment that
never touches the setting". `DEV-LOGIN-STATUS-LEAK` and `INFO-STATUS-BEFORE-SETUP` both record a
deliberate disclosure (`AuthStatusResponse.HasUsers` / `DevelopmentAuthenticationEnabled`, exposed to
every anonymous caller) rather than treating it as a finding - the client needs exactly this
information to decide whether to show a setup form or sign itself in without a login form.

**Disclosure**: `INFO-SESSION-CLAIMS` is a real finding. `SignInManager.CreateUserPrincipalAsync`
adds a security-stamp claim (`IdentityOptions.ClaimsIdentity.SecurityStampClaimType`, read from the
host's own options rather than hardcoded) to the same principal that both mints the access token and
answers `GET api/auth/session` - so the stamp value is embedded in the token and echoed back to the
client verbatim. `INFO-RESPONSE-HEADERS` is a real, if minor, finding of a different kind: Kestrel's
own default (`Server: Kestrel`) is present because nothing in this harness or the framework it hosts
ever calls `KestrelServerOptions.AddServerHeader = false`. `INFO-ERROR-BODIES` sends malformed
bodies, a wrong content type, an unsupported method, and malformed JSON across several auth routes
and finds no stack trace, exception type, or framework identifier in any response body - ASP.NET
Core's own `ProblemDetails` handling already covers every one of these cases.

**Client pipeline** (`Probes/ClientPipeline/`): `ClientPipelineSupport.BuildClientStack` composes the
real handlers - `AuthTokenHandler` chained ahead of `SessionExpiryHandler`
(`authTokenHandler.InnerHandler = sessionExpiryHandler`), a real `BlazorBaseUserAuthStateProvider`,
and `BlazorBase.User.Wasm`'s own `InMemoryTokenStorage` - exactly the way `BlazorBase.User.md`'s
own client-registration section wires them, including a *separate*, un-augmented `HttpClient` for
`IAuthService` (its own doc comment: attaching `AuthTokenHandler` to it would close a dependency
cycle, and its endpoints are anonymous and need no bearer anyway). `CLIENT-REFRESH-ON-401`,
`CLIENT-NO-RETRY-STORM` and `CLIENT-CONCURRENT-USERS` all force the token to expire on the *server*
only, by advancing `HarnessTimeProvider` after login rather than before - the client's own
`ExpiresAt` was read from the same, pre-advance clock, so it still looks fresh to the client's real
wall clock, and only the wire produces the 401. This is the same clock-advance technique
`TOKEN-STAMP-INVALIDATION` (step 3) already proved works for access-token expiry.

`CLIENT-REFRESH-ON-401` is the canary, confirmed by a request-counting `DelegatingHandler`
(`RefreshCallCountingHandler`) wired into `IAuthService`'s own client: exactly one call to
`api/auth/refresh`, then the original request succeeds on retry. `CLIENT-NO-RETRY-STORM` revokes the
refresh-token family directly (`AuthTokenIssuer.RevokeFamilyAsync`, resolved from the profile's own
service provider) before the call, so the one refresh attempt is guaranteed to fail - and confirms
`AuthTokenHandler` gives up after that one attempt rather than looping, and that
`BlazorBaseUserAuthStateProvider.MarkUserAsLoggedOut()` actually clears the token storage.
`CLIENT-CONCURRENT-USERS` is the reason this group exists: `AuthTokenHandler.RefreshGate` is a
`static SemaphoreSlim`, shared by every instance in the process - correct for WebAssembly and MAUI,
where a process is one user, but shared by every circuit in a Blazor Server host. Two separately
composed client stacks, two different users, both access tokens expired the same way, refreshing at
the same time: the probe confirms via `harness/whoami` (with each client's own final access token
attached - the harness's own bug here, described below, briefly hid this) that neither client ends up
with the other's identity, only that the shared gate serialises them.

`CLIENT-401-INVALIDATES` is the other real, non-trivial finding in this step, not a harness defect:
composed exactly as `BlazorBase.User.md` documents it, `BlazorBaseUserAuthStateProvider` never
becomes authenticated under the `Bearer` transport at all, even immediately after a successful login.
`GetAuthenticationStateAsync()` always resolves the principal through `IAuthService.GetSessionAsync()`,
and `IAuthService`'s own `HttpClient` deliberately carries no `AuthTokenHandler` - so that call is
always made without the access token and always answers anonymous. The 401-invalidation behaviour
this probe set out to test never gets a chance to matter, because the state it would invalidate never
becomes authenticated through this composition in the first place. The framework's own
`BlazorBaseUserAuthStateProviderTests` mask this with a stub `IAuthService` that returns a canned
authenticated session regardless of transport, never exercising the real `AuthService.GetSessionAsync()`
implementation this probe drives over real HTTP.

`CLIENT-TOKEN-STORAGE` needed a `BlazorBase.User.Wasm` project reference (`InMemoryTokenStorage`,
`BrowserTokenStorage`) - the only new reference this step adds. It only inspects which `ITokenStorage`
each registration method selects, through a bare `ServiceCollection` and a throwing stub `IJSRuntime`
that satisfies `BrowserTokenStorage`'s constructor without ever being invoked: `AddBlazorBaseUserWasm`
defaults to `InMemoryTokenStorage`, switches to `BrowserTokenStorage` (`localStorage`, script-readable)
only when a host opts in with `persistTokensInBrowserStorage: true`, and
`AddBlazorBaseUserWasmCookieAuth` registers `NoOpTokenStorage`.

**Harness bug found and fixed during this step**: `ClientPipelineSupport.WhoAmIAsync` originally called
`harness/whoami` with no bearer token at all, even though that endpoint requires authorization. The
first run of `CLIENT-CONCURRENT-USERS` reported both clients' final identity as `(none)`, which looked
exactly like a cross-user token leak but was actually the probe never authenticating its own
verification call. Fixed by attaching each client's own final access token to the `whoami` call; the
probe now passes, correctly confirming the shared gate only serialises the two users.

## Running it

```powershell
dotnet run --project Libs/BlazorBase.User.SecurityHarness -- --list
dotnet run --project Libs/BlazorBase.User.SecurityHarness -- --profile all
dotnet run --project Libs/BlazorBase.User.SecurityHarness -- --profile Bearer --format markdown
dotnet run --project Libs/BlazorBase.User.SecurityHarness -- --profile all --format json --output report.json
```

### Command line

| Option | Meaning |
|---|---|
| `--profile <name\|all>` | Which host profile(s) to start. See `Hosting/HarnessProfile.cs`. |
| `--category <name>` | Only run probes in this category. |
| `--id <probe-id>` | Only run the probe with this id. |
| `--format console\|markdown\|json` | Report format. Defaults to `console`. |
| `--output <path>` | Write the report to a file instead of standard output. |
| `--base-url <url>` | Run only against an already-running, foreign host - see the note below. |
| `--list` | Print the probe catalogue without starting any host. |

Exit code `0` when every probe passed or was skipped, `1` when any probe failed, `2` on a harness
error (a host that could not be built, an unhandled exception in the tool itself).

### `--base-url`

No probe in the catalogue currently declares itself safe to run against a host the harness did not
start itself, so `--base-url` today produces a valid, empty report rather than a guess. A later
step that adds a black-box-safe probe extends this switch's meaning at the same time it adds that
probe - `IAuthProbe` stays exactly as declared today until there is a real reason to grow it.

## Host profiles

One real Kestrel host per row, each on an operating-system assigned loopback port:

| Profile | What it wires up |
|---|---|
| `Bearer` | `AddBlazorBaseUserServer` with defaults, over HTTP. |
| `BearerHttps` | The same, over HTTPS with a self-signed loopback certificate. |
| `Cookie` | `AddBlazorBaseCookieAuthTokens` plus `app.UseBlazorBaseCsrf()`, over HTTPS. |
| `CookieWithoutCsrfMiddleware` | The cookie transport with `app.UseBlazorBaseCsrf()` deliberately not called. |
| `External` | `AddBlazorBaseExternalAuth` pointed at the fake identity provider started alongside every run (see "Fake identity provider" below), over HTTPS - required for the redirect flow's `SameSite=None` correlation cookie. |
| `AccessTokens` | `AddBlazorBaseAccessTokens` with defaults. |
| `DevelopmentLogin` | `AddBlazorBaseDevelopmentAuthentication` in the Development environment. |
| `ProductionWithDevelopmentLogin` | The same registration with `EnvironmentName = "Production"`. |

`CookieWithoutCsrfMiddleware` and `ProductionWithDevelopmentLogin` are expected to fail: the
framework's own startup guards exist precisely to catch these two misconfigurations. The report
records the guard's exact message rather than treating the failure as a harness error.

The self-signed certificate is generated in-process for every run and never touches a machine
certificate store. A probe's HTTP client trusts exactly that certificate's thumbprint - never a
blanket `_ => true` validation callback.

## Fake identity provider

A second real Kestrel host, started before any profile and stopped after every profile has run,
serves an actual OpenID Connect discovery document and an actual JWKS with two rotatable RSA key
pairs (`FakeSigningKeyRegistry.KeyIdA`, published by default, and `KeyIdB`, withheld by default).
`OidcExternalTokenValidator` runs against it completely unmodified: `HarnessHostFactory` registers a
`BaseConfigurationManager` (`PinnedThumbprintConfigurationManagerFactory`) built over an
`HttpClient` that trusts exactly the provider's certificate thumbprint, and the validator's
constructor picks that instance up from dependency injection instead of building its own. Every
part of the validator - signature verification, issuer and audience validation, `tid` handling -
still executes; only the transport is pinned.

`FakeTokenBuilder` hand-assembles compact JWS tokens from base64url segments, including shapes a
well-behaved library will not produce on request: `alg: none`, an HS256 token whose secret is an
RSA public key (algorithm confusion), a header `kid` that names a different key than the one that
actually signed the token, and any combination of missing, expired or out-of-range claims a
`FakeTokenDescription` can express.

The `External` profile registers two `IExternalUserResolver<HarnessUser>` implementations behind a
runtime switch (`ExternalUserResolverSwitch`, resolved from the host's own DI container): the
strict `StrictSubjectExternalUserResolver`, matching on the provider's `oid`/`sub` claim, and the
deliberately unsafe `NaiveEmailMatchingExternalUserResolver`, matching on the `email` claim without
checking `email_verified` - named and documented so it is unmistakably a demonstration, never a
recommendation.

A rotation-relevant note: `ConfigurationManager<T>`'s default fire-and-forget background refresh
means a `RequestRefresh()`-triggered retry inside one failed validation call still sees the stale
configuration, since the background fetch has not completed yet. `PinnedThumbprintConfigurationManagerFactory`
sets the library's `Switch.Microsoft.IdentityModel.UpdateConfigAsBlocking` `AppContext` switch so a
refresh blocks instead - a supported, library-wide setting, not a change to the validator.

## Adding a probe

1. Add a class under `Probes/` implementing `IAuthProbe`.
2. Give it a public parameterless constructor - the catalogue constructs probes by reflection.
3. State the attack, the expectation, and the severity if the expectation fails, in the probe's own
   properties. A failing probe should attach the request/response pair that proved it as
   `ProbeEvidence`.
4. Nothing else needs registering; `dotnet run -- --list` picks it up on the next build.

## Design notes

- Real Kestrel, not `TestServer`: header casing, cookie attribute parsing, the `__Host-` prefix and
  connection-level behaviour are only observable over a genuine socket.
- Each profile gets its own file-backed SQLite database in a per-run temporary directory, not
  `DataSource=:memory:` - an in-memory SQLite database only lives as long as the single connection
  that opened it, and Kestrel serves requests from pooled connections on several threads.
- The two framework startup guards this step exercises
  (`BlazorBaseCsrfStartupGuardFilter`, and the analogous guard for a host that never registers a
  concrete auth controller or forgets `MapBlazorBaseAuthSessionEndpoint()`) run as per-request
  middleware, not during `host.StartAsync()`. The harness sends one harmless request to each newly
  started host and inspects that host's own logs for the guard's exception before declaring it
  "started" - a real and worth-noting difference from a build-time failure, reported precisely
  rather than smoothed over.
- The harness observes the framework; it never changes it. Every framework file referenced here is
  read-only from this project's point of view.
