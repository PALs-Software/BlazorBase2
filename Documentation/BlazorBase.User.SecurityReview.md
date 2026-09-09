# BlazorBase.User.Server — security review

A critical review of the BlazorBase2 authentication stack, written from what an executable attack
harness actually demonstrated against it — not from reading the source and reasoning about what
should happen. The harness lives in
[`BlazorBase.User.SecurityHarness`](../BlazorBase.User.SecurityHarness/README.md), starts genuine
Kestrel hosts for every configuration the framework supports, and drives them over genuine HTTP with
a catalogue of 117 adversarial probes. This document is the report the harness exists to produce.

A prior round (`.plan/auth-review/`, not part of this repository's published documentation) reviewed
the same stack by reading the source and reasoning about reachability. That round rejected three
findings as unreachable, on the strength of dependency-injection wiring alone. This round overturns
two of those speculative expectations outright (see "Two findings that were investigated and
killed", below) and demonstrates a fourth, structural defect the reading-based round never
considered, because a reading-based review cannot distinguish "unreachable" from "not yet reached."

---

## Method and how much each finding is worth

Every finding below carries one of three evidence classes. A reader who cannot tell them apart cannot
weigh them correctly, so the label always comes first.

- **Demonstrated** — a named probe reproduces the effect directly. The probe id lets anyone re-run
  it against a current build (`dotnet run --project Libs/BlazorBase.User.SecurityHarness -- --profile
  all`) and see the same request and response.
- **Measured** — a number, produced under stated conditions (sample size, host configuration, clock
  handling). A measurement is not a verdict; where the harness itself reports "Inconclusive" it is
  usually because the result is a number rather than a pass/fail, not because the probe failed to
  run.
- **Reasoned** — read from the framework's own source, not exercised by any probe. Marked as such
  wherever it appears; it is not worth less by definition, but it has not been attacked.

A run currently produces 92 passes, 10 hard failures and 15 measurements out of 117 probes. The
[baseline file](../BlazorBase.User.SecurityHarness/Baseline/known-findings.baseline) is the
machine-readable form of the same 10 failures listed below; CI fails only on a probe that regresses
past that baseline or a baseline entry naming a probe that no longer exists.

---

## Findings

Ordered by severity: Critical, High, Medium, Low, Info. Several findings share one root cause and are
grouped under it rather than counted separately.

### Critical — the browser redirect / BFF sign-in flow cannot ever succeed against a real identity provider

**Demonstrated** (`REDIR-COMPLETE-REPLAY`, `REDIR-RETURNURL-PRESERVED`, `REDIR-OPEN-REDIRECT`).

`ExternalAuthRedirectEndpointMapper.Complete` calls `httpContext.AuthenticateAsync(externalScheme)` to
read back the identity the OIDC handshake just established, exactly as its own XML documentation
prescribes. Against a real `RemoteAuthenticationHandler` (`AddOpenIdConnect`, `AddMicrosoftIdentityWebApp`,
or any other remote scheme), that call cannot succeed: a remote handler only produces a principal while
it is processing its own callback request, and answers every other call — including this one — with an
authentication failure. The harness drove the full handshake against a real fake identity provider,
including a real authorization code exchange over HTTPS with a certificate-pinned backchannel, and
`Complete` rejected the completed flow every single time, at exactly this call.

The three probes built to attack what happens *after* a completed redirect — replay of the completion
URL, preservation of the original `returnUrl`, and the eleven-payload open-redirect battery — are all
`Inconclusive` for the same reason: the flow never reaches the code they were built to attack, because
it never completes at all. **This is the most consequential result in the harness**, not because a
probe failed, but because it shows a documented, shipping feature does not function as designed. The
framework's own test suite (`ExternalAuthRedirectEndpointMapperTests`) does not catch this because it
substitutes a synthetic scheme whose `HandleAuthenticateAsync` trivially succeeds — nothing like a real
remote handler.

**Reachability**: PlatformSubscriptions does not use this endpoint pair; it uses the token-exchange
endpoint (`api/auth/external/exchange`) exclusively (see "The consuming application", below). Any host
that has wired the redirect flow exactly as documented is currently unable to sign a single user in
through it.

**What should change**: the framework needs to capture the external principal during the OIDC handler's
own callback processing — for example through `OpenIdConnectEvents.OnTicketReceived` — rather than
re-authenticating the external scheme afterward. This is not a place to swap in a Microsoft library
instead of hand-written code; the hand-written code is misusing a contract the library already
enforces correctly. See "The library question", below.

### Critical / High (grouped) — long-lived credentials outlive the events meant to end them

One root cause, four demonstrations, at different severities depending on which credential and which
event:

- **`ACCT-RESET-INVALIDATES-SESSIONS`** (Failed, severity Critical) — **Demonstrated.** A password
  reset rotates the security stamp and revokes the refresh token immediately: presenting it right
  after the reset is rejected (401). The access token issued before the reset is not re-checked
  against the stamp at all, and kept authenticating (200) immediately after the reset, only failing at
  its own natural expiry. A password reset is exactly the moment a user acts because they believe
  themselves compromised, and the access token survives it in full.
- **`COOKIE-LOGOUT-CLEARS`** (Failed, severity High) — **Demonstrated.** Logout tells the browser to
  drop the session cookie (`Set-Cookie` with an expired date) but never rotates the security stamp or
  otherwise revokes that cookie value server-side. The pre-logout cookie value, replayed manually,
  still authenticates. The bearer transport's logout does revoke the refresh-token family, so the two
  transports do not offer equivalent guarantees at logout.
- **`AT-INACTIVE-OWNER`** (Failed, severity High) — **Demonstrated.** `AccessTokenService.ValidateAsync`
  re-checks `IsRevoked` and `ExpiresAt` against the database on every call, but never the owning
  account's `IsActive` flag. A machine access token keeps authenticating after the account it belongs
  to is deactivated — the first lever an administrator reaches for does not close this door.
- **`TOKEN-STAMP-INVALIDATION`** (Inconclusive, severity if failed Medium) — **Measured.** Rotating the
  security stamp by itself (without a reset) adds zero seconds of protection to an already-issued
  access token; it keeps working until its own expiry (a step-3 finding, restated here because
  `ACCT-RESET-INVALIDATES-SESSIONS` demonstrates the same shape at a moment that actually matters).

The shared cause: an access token, once minted, is validated only against its own signature and
expiry (`AuthTokens:AccessTokenMinutes`, 15 minutes by default). Nothing that happens afterward —
stamp rotation, sign-out, password reset, account deactivation for a machine token — is re-checked
against a live access token. Revocation reaches the long-lived, database-backed credential (the
refresh token, or the cookie's sliding-expiration state) and never the short-lived one that is,
deliberately, not database-backed at all.

**What should change**: see "Recommendations" — this is a design trade-off (an opaque access token
costs nothing to validate; a per-request stamp check costs a database round trip), not an oversight,
and the right answer is a host-configurable choice with the exposure window stated plainly, not a
silent default.

### Medium — an absent `Sec-Fetch-Site` header is treated as same-origin

**Demonstrated** (`CSRF-NO-FETCH-METADATA`). `BlazorBaseCsrfChecks.IsSameOriginRequest` treats a
request that carries no `Sec-Fetch-Site` header at all the same as one that carries `same-origin` or
`none` — by the check's own doc comment, deliberately, because an absent header means a client that
sends no fetch metadata, which includes every browser making a same-origin request through an
intermediary that strips the header, and also every non-browser HTTP client. A request with a valid
antiforgery token and no fetch-metadata header at all is accepted. The consequence: for any client
that omits `Sec-Fetch-Site` (`curl`, most non-browser tooling, some proxies), the antiforgery token is
the *only* defence in play — the fetch-metadata half of the control protects browsers specifically and
nothing else. This is documented framework behaviour, not a bug the code fights against, but it is
worth a host knowing plainly rather than assuming both halves of the defence are always active.

### Medium — the session endpoint echoes the security-stamp claim to the client

**Demonstrated** (`INFO-SESSION-CLAIMS`). `SignInManager.CreateUserPrincipalAsync` adds the security
stamp (`AspNet.Identity.SecurityStamp`) to the same principal that both mints the access token and
answers `GET api/auth/session`, so the raw stamp value is returned to any authenticated caller. Nothing
in the framework filters it before serialising `AuthSessionResponse.Claims`. The stamp is not a secret
in the sense of being independently exploitable (it identifies the *validity generation* of the
account, not a credential), but it is internal bookkeeping with no client-side use, and there is no
principled reason to hand it out.

### Medium — the external token-exchange endpoint has no replay protection

**Measured** (`EXT-REPLAY`, `EXT-EXPIRED-WITHIN-SKEW`). The same valid, unexpired provider token
exchanged five times in a row minted five distinct local sessions in one run; the harness did not find
a ceiling. There is no `jti` or nonce tracking on `api/auth/external/exchange` at all — a provider
token, once obtained, is a session factory for as long as it remains valid, which is also thirty
seconds longer than its stated `exp` because of the validator's clock-skew allowance
(`EXT-EXPIRED-WITHIN-SKEW`: a token expired 25 seconds ago is still accepted; one expired 35 seconds
ago is not). The harness's own a-priori severity tag for this probe was Info, on the reasoning that it
is a measurement rather than a pass/fail check; this review rates the underlying gap Medium rather than
Info, because the practical effect — an intercepted or leaked provider token producing unlimited local
sessions — is a real amplification of whatever exposure the provider token already represents, and
because this repository's own configuration (external auth pointed at the multi-tenant `/common`
authority) widens the pool of tokens that could reach this endpoint. See "The consuming application",
below.

### Medium — the framework ships no default external-user resolver, and the naive one a host is likely to write is exploitable

**Demonstrated** (`EXT-UNVERIFIED-EMAIL-TAKEOVER`; confirmed safe alternative in
`EXT-STRICT-RESOLVER-REJECTS`). `BlazorBase.User.Server` ships **no default**
`IExternalUserResolver<TUser>` — matching an external identity to a local account is entirely a host
responsibility. The harness built the two resolvers a host is realistically choosing between: one
that matches on the provider's `sub`/`oid` (`StrictSubjectExternalUserResolver`), and one that matches
on the token's `email` claim (a plausible first attempt, since email is the field a person recognises).
Against the strict resolver, a token with an unrecognised subject and someone else's `email` claim
(`email_verified: false`) is rejected. Against the naive email-matching resolver, the same token takes
over the existing account belonging to that email address — confirmed via a `harness/whoami` call
against the minted token, which returns the victim's own identity. **This is not a framework defect**:
the framework offers no unsafe default to misuse, because it offers no default at all. It is a gap in
guidance — the naive shape is exactly what a host writes when given no example of a safe one — and it
belongs in this review because the consequence (full account takeover) is severe wherever a host
writes the naive version. PlatformSubscriptions's own resolver does not have this shape; see "The
consuming application", below.

### High — a two-factor pending token has no single-use tracking

**Demonstrated** (`2FA-PENDING-REPLAY`). A pending two-factor token, once used to complete a login
successfully, can be presented again with a freshly computed TOTP code and mints a second, independent
session. `TwoFactorPendingTokenService` relies solely on the token's own data-protection expiry; it
carries no record of having already been consumed. Two-factor is meant to be a single completed
exchange per login attempt; here it is a window that stays open until the pending token's own
lifetime runs out, for anyone who can produce a valid code more than once (which the legitimate device
holder always can, every 30 seconds, by construction).

### High — the client's cached authentication state never becomes authenticated under the bearer transport

**Demonstrated** (`CLIENT-401-INVALIDATES`). The probe set out to test whether a 401 drops a cached
authenticated principal; it found that the principal never becomes authenticated in the first place.
`BlazorBaseUserAuthStateProvider.GetAuthenticationStateAsync` always resolves the principal through
`IAuthService.GetSessionAsync()`, and `IAuthService`'s own `HttpClient` deliberately carries no
`AuthTokenHandler` — the framework's own registration comment states the reason: the auth endpoints
are anonymous and attaching the handler there would close a dependency cycle. The consequence is that
`GET api/auth/session` is always called without the caller's access token, and always answers
anonymous, even immediately after a successful login composed exactly the way
[`BlazorBase.User.md`](BlazorBase.User.md)'s own client-registration section documents it. The
framework's own `BlazorBaseUserAuthStateProviderTests` substitute a stub `IAuthService` that returns a
canned authenticated session regardless of transport, and never exercise the real implementation over
HTTP, which is why this has stayed invisible. This is a functional defect in client-side UI state (menu
visibility, route guards), not an authorization bypass — the server re-checks every request regardless
of what the client believes about itself, and `BlazorBase.User.md`'s own doc comment says exactly that.

### Low — Kestrel's default `Server` header is present

**Demonstrated** (`INFO-RESPONSE-HEADERS`). Every response carries `Server: Kestrel`. This is
Kestrel's own default (`KestrelServerOptions.AddServerHeader` is never set to `false`, by the harness
or by the framework it hosts); it belongs to host configuration, not to `BlazorBase.User.Server`
itself. Included for completeness, not because it is framework-owned.

### Not findings — deliberate behaviour confirmed, not a defect

- **`AT-CASE-AND-TRIM`**'s whitespace-tolerance half: `AccessTokenAuthenticationHandler.ExtractToken()`
  explicitly `.Trim()`s the raw header value before comparison — the padded variant is byte-identical
  to the valid secret by the time it is compared, never a weakened check against a *different* secret.
- **`CSRF-REPLAY`**: antiforgery tokens are not single-use; they validate repeatedly, exactly as
  ASP.NET Core's antiforgery tokens are designed to.
- **`ACCT-CONFIRM-REUSE`**: an email-confirmation token is reusable, because confirming an email does
  not rotate the security stamp the token's validation is bound to — unlike a password-reset token,
  which is. Recorded as Inconclusive by the harness on purpose: there was no a-priori reason to expect
  either direction.
- **`DEV-LOGIN-STATUS-LEAK`**, **`INFO-STATUS-BEFORE-SETUP`**: `api/auth/status` tells an anonymous
  caller whether any account exists and whether development login is enabled. Both are needed for a
  first-run client to decide whether to show a setup form or sign itself in without one; `Development
  AuthenticationEnabled` is always `false` outside the Development environment.
- **`COOKIE-BEARER-DOWNGRADE`**: an arbitrary `Authorization: Bearer` header does bypass the CSRF
  middleware check (by design — bearer requests are not the CSRF-vulnerable case), but the policy
  scheme's `ForwardDefaultSelector` then forces bearer-only authentication for that request, so it
  never rides on the cookie's identity. Safe.

---

## Confirmed safe

The point of an executable harness is the coverage claim it can defend, not only the list of things it
found wrong. Everything below was attacked and held.

**External token exchange** — the entire forgery battery against `api/auth/external/exchange` was
rejected without exception: `alg: none`, algorithm confusion against five different encodings of the
provider's own RSA public key used as an HMAC secret, an unknown signing key, a `kid` naming one key
while a different key signed, a missing signature, a tampered payload, a wrong audience, a wrong
issuer, a missing or non-allow-listed `tid` (including a real-but-unlisted tenant), a missing required
scope, an expired token (outside the clock skew), a not-yet-valid token, a token with no `exp` claim at
all, and a 2 MB oversized token (rejected in 24 ms, no unbounded work). `OidcExternalTokenValidator`
holds against every shape the harness could construct.

**CSRF and cookie transport** — every state-changing request without the antiforgery header is
rejected (403); a token minted for a different session is rejected; `Sec-Fetch-Site: cross-site` and
`same-site` (at the framework's own default) are rejected even with a valid token; `same-origin` is
correctly accepted. The startup guard for a host that registers the cookie transport without calling
`app.UseBlazorBaseCsrf()` refuses to serve at all, naming the exact missing call. The session cookie
carries `HttpOnly`, `Secure`, `SameSite=Lax`, and the `__Host-` prefix with every attribute that prefix
requires. The login and session responses carry no access or refresh token in the body under the
cookie transport, and `Cache-Control: no-store` on every token-bearing response. A refresh token
supplied in the request body has no effect on the cookie transport — the cookie is the only session.

**Token lifecycle** — refresh-token replay is detected and revokes the entire family, not only the
replayed token; exactly one of five concurrent refreshes with the same token succeeds; a refresh token
is never accepted where an access token is expected and vice versa; the `Authorization` header cannot
override which user's refresh token is actually rotated; malformed, truncated, empty and 1 MB token
values are all rejected cleanly with no server error; logout answers identically for a live, an
already-revoked and a never-issued token, so logout is not an existence oracle.

**Rate limiting and lockout** — login, forgot-password, development-login, the external challenge
endpoint and the external exchange endpoint all enforce their configured permit limits with 429; an
account locks after repeated wrong passwords and unlocks once the window passes; a flood of requests
carrying a distinct, spoofed `X-Forwarded-For` value on every request is still rejected at the limit
— the limiter partitions on the socket address, not a client-controlled header, unless the host
explicitly configures forwarded-header trust.

**Timing side channels** — none demonstrated. Login (known-account-wrong-password vs. unknown user),
the machine-access-token prefix check, and both account-recovery endpoints (forgot-password,
resend-confirmation) were all measured with an interleaved sampler and a bootstrap confidence interval
on the median difference; every interval included zero.

**Account management** — a user's confirmation or reset token is rejected when presented with a
different user's id or email; a reset token is single-use because the reset it performs rotates the
same security stamp its own validation is bound to; an expired reset token is rejected; an
11-character password is rejected against the 12-character policy; disabling two-factor or
regenerating recovery codes both require a correct code or recovery code and reject an empty or wrong
one.

**Two-factor** — a pending token belonging to one user is rejected when paired with another user's
code; an expired pending token is rejected; a recovery code is rejected on its second use; wrong TOTP
codes are not separately rate-limited but do count toward Identity's own lockout.

**Development login** — absent (404) in Production when never registered; absent when explicitly
disabled in Development; rate limited like every other auth endpoint. A host configured as Production
with development authentication enabled fails at startup rather than serving it.

**Client pipeline** — the static refresh gate shared by every circuit in a Blazor Server process only
serialises concurrent refreshes; confirmed via `harness/whoami` that no token crosses between two
users refreshing at the same time. A guaranteed-to-fail refresh triggers exactly one attempt and a
sign-out, never a retry loop. A normal expired-access-token call triggers exactly one refresh and one
retry, then succeeds.

**Disclosure** — no response body across malformed JSON, wrong content type, unsupported method or
malformed input carries a stack trace, an exception type or a framework version string.

---

## Two findings that were investigated and killed

A review that only ever adds findings is not trustworthy. Two speculative expectations from earlier
steps were tested directly and did not hold up.

1. **"Blocking configuration refresh makes a rotated signing key visible on the very first request."**
   Step 2's doc comments raised this as the expected difference between the library's default
   fire-and-forget refresh and its blocking mode. Measured directly (`EXT-KEY-ROTATION`): the rotated
   key was accepted on the second request in *both* modes, in this harness run. The expectation did
   not hold; recorded as a genuine measurement, not adjusted after the fact.
2. **"A `ConfigurationManager` that fails while the provider is offline stays locked out for a period
   once the provider recovers."** Measured directly (`EXT-OFFLINE-THEN-ONLINE`): a cold configuration
   cache that failed while the provider was down recovered on the very first request after the
   provider came back online, 11 ms later. No lockout period was observed.

Neither of these was ever presented as a finding; they are recorded here specifically because the plan
that produced this harness called for it, and because a review that silently drops disproven
speculation is indistinguishable from one that never checked.

---

## The library question

Per component, as the review was asked to answer it: is there a first-party Microsoft alternative that
would genuinely replace the hand-written code, and is switching worth it?

- **Token validation, issuer validation, JWKS handling.** Already `Microsoft.IdentityModel.*`
  (`JsonWebTokens`, `Tokens`, `Protocols.OpenIdConnect`, `Validators`) plus
  `Microsoft.AspNetCore.Authentication.JwtBearer`. Nothing hand-written sits between a token and its
  cryptographic verification. The full forgery battery above held against every attack the harness
  could construct. **No change; this is already the right answer.**
- **The refresh-token family, rotation and reuse-detection.** Hand-written (`AuthTokenIssuer<TUser>`).
  No first-party ASP.NET Core Identity surface implements OAuth 2.0 Security BCP rotation with
  family-based reuse detection — `AddBearerToken`/the Identity API endpoints issue a token pair but do
  not track families or detect replay. **Keep hand-written.** The demonstrated behaviour
  (`TOKEN-REFRESH-ROTATES`, `TOKEN-FAMILY-REVOKED`, `TOKEN-REFRESH-CONCURRENT`) is correct and there is
  nothing supported to replace it with.
- **The transport abstraction and the cookie transport.** Hand-written over Identity's own
  `AddBearerToken` and cookie authentication primitives (`SignInManager`, `IdentityConstants
  .ApplicationScheme`). This glue — choosing which of two already-first-party mechanisms delivers a
  token, and doing so behind one interface — is inherently host-specific; there is no library that
  ships this choice. **Keep hand-written.**
- **The CSRF check.** `IAntiforgery` (first-party) plus a hand-written `Sec-Fetch-Site` fetch-metadata
  check. ASP.NET Core has no built-in fetch-metadata check; this is necessarily custom. **Keep
  hand-written**, but see the Medium finding above about the absent-header case — worth tightening,
  not worth replacing.
- **The client handler pipeline and the authentication state provider.** Hand-written
  (`AuthTokenHandler`, `BlazorBaseUserAuthStateProvider`). No first-party `DelegatingHandler` or
  `AuthenticationStateProvider` performs proactive-refresh-then-reactive-401 token renewal for a Blazor
  client; this is an application concern ASP.NET Core does not attempt to solve generically. **Keep
  hand-written**, but fix the two demonstrated gaps above (`CLIENT-401-INVALIDATES`, and the
  `RefreshGate` scoping warning already documented in code but easy to miss).
- **Machine access tokens.** Hand-written (`AccessTokenService`, prefix + hash, à la a GitHub personal
  access token). No Microsoft library issues this shape of long-lived, revocable, database-backed API
  credential; it is a well-understood, correctly-structured pattern. **Keep hand-written**, but add the
  missing `IsActive` check (`AT-INACTIVE-OWNER`).
- **The external redirect endpoints.** Hand-written over a registered, first-party OIDC handler
  (`AddOpenIdConnect`). This is the one component where "reinvented" is not the right word for the
  problem: the library handler is doing exactly what it is supposed to, and the hand-written endpoint
  is calling it incorrectly (`AuthenticateAsync` outside callback processing, which no remote handler
  supports). **Do not replace the library piece — fix the hand-written call.** The correct fix keeps
  the OIDC handler as-is and captures the principal during its own callback (`OnTicketReceived` or
  equivalent), which is exactly the extension point the library already offers for this.

No component surveyed here is a case of "should be a library instead and currently is not." Every
hand-written piece is hand-written because no supported first-party alternative does what it does;
the defects found are logic gaps and one contract misuse, not wrong build choices.

---

## The consuming application

`Src/PlatformSubscriptions.Server` (`Auth/AuthExtensions.cs`, `Program.cs`) does not use
`BlazorBase.User.Server`'s bearer or cookie transport for interactive sign-in at all: its own
`AddPlatformAuth` registers a policy scheme that forwards machine-token-shaped credentials to
`AccessTokenAuthenticationDefaults.AuthenticationScheme` and everything else to a directly-registered
`AddJwtBearer` handler validating Entra JWTs per request. It does use the framework's external
token-exchange machinery (`AddBlazorBaseExternalAuth`, `MapBlazorBaseExternalAuthEndpoints`),
**pointed at the multi-tenant `https://login.microsoftonline.com/common/v2.0` authority** — the
authority every finding above about `api/auth/external/exchange` bears on directly, because `/common`
means "any Microsoft account that exists" rather than a single known tenant.

Two things in this repository's own configuration are worth recording precisely because of that:

1. **`PlatformExternalTenantAllowList`** replaces the framework's default, static-list-backed allow
   list with one backed by real rows in `PlatformDbContext.Tenants` — registered *before*
   `AddBlazorBaseExternalAuth` specifically so the framework's `TryAdd` does not win. This is the
   compensating control the `/common` authority needs, and it is already in place.
2. **`PlatformExternalUserResolver`** resolves an external identity by `AppUser.ExternalId` — the
   provider's subject — through `PlatformExternalIdentityResolver`, the same resolution
   `EntraClaimsTransformation` applies per request for the JWT path. It is shaped like the harness's
   *strict* resolver, not the naive email-matching one `EXT-UNVERIFIED-EMAIL-TAKEOVER` demonstrates
   taking over an account. **This repository is not exposed to that finding as it stands.** Any future
   change to this resolver should preserve subject-based matching rather than falling back to `email`.

What this repository has not compensated for, because the gap is in the framework rather than in
application wiring: the replay finding on `api/auth/external/exchange` (no `jti`/nonce tracking) and
the access-token-outlives-revocation group both apply here exactly as described above — a `/common`
authority widens who can reach the exchange endpoint in the first place, which is why the replay
finding is rated Medium rather than Info in this review despite the harness's own lighter tag on the
underlying measurement probe.

The redirect/BFF flow (the one demonstrated non-functional above) is not used here; this repository's
only external-auth entry point is the token-exchange endpoint.

---

## Recommendations

Framework changes first, then decisions a consuming application must make for itself. Anything
affecting a host pointing external auth at `/common` — as this repository does — is called out.

### Framework (`BlazorBase.User.Server` / `BlazorBase.User`)

1. **Fix `ExternalAuthRedirectEndpointMapper.Complete`.** It cannot mint a session against any real
   remote authentication handler today. Capture the principal during the OIDC handler's own callback
   processing instead of re-authenticating the external scheme afterward. Until this lands, the
   redirect/BFF flow should not be presented as a supported alternative to the token-exchange endpoint.
2. **Add replay/single-use protection to `api/auth/external/exchange`** — a `jti` or nonce check
   against a short-lived store. This matters most for any host on `/common`, including this one.
3. **Check the owning account's `IsActive` in `AccessTokenService.ValidateAsync`.** Closes
   `AT-INACTIVE-OWNER` with a one-line addition to a check the code already performs for `IsRevoked`
   and `ExpiresAt`.
4. **Add single-use tracking to `TwoFactorPendingTokenService`.** Closes `2FA-PENDING-REPLAY`.
5. **Give the cookie transport a server-side revocation path on logout** — at minimum, rotate the
   security stamp on logout the way a password reset already does, so a captured pre-logout cookie
   value stops working immediately rather than at the next sliding-expiration renewal.
6. **Filter the security-stamp claim out of `AuthSessionResponse.Claims`** (or any other claim the
   client has no use for) before it is serialised. Closes `INFO-SESSION-CLAIMS`.
7. **Fix or document `CLIENT-401-INVALIDATES`.** Either give `IAuthService`'s `HttpClient` a way to
   attach the caller's bearer token when one exists (breaking the documented dependency-cycle
   avoidance deliberately), or update `BlazorBase.User.md` to state plainly that
   `BlazorBaseUserAuthStateProvider` never reports "authenticated" under the bearer transport as
   currently composed, so a host relying on it for UI gating knows before shipping, not after.
8. **Ship a documented, safe default `IExternalUserResolver<TUser>`** (subject/`oid`-based, matching
   the harness's own strict resolver and this repository's `PlatformExternalUserResolver`), so a host
   without one is not the one writing the naive email-matching version from scratch.
9. **Decide, and document, the access-token revocation trade-off explicitly.** Today an opaque access
   token costs nothing to validate beyond its own signature and expiry, and nothing that happens after
   issuance — stamp rotation, logout, password reset — shortens its life. That is a legitimate design
   choice (a per-request stamp check costs a database round trip), but it should be a documented,
   host-configurable one rather than an implicit default a reader has to reconstruct from four separate
   probes.

### Consuming applications (including PlatformSubscriptions)

1. **If pointing external auth at `/common`** — as PlatformSubscriptions does — do not treat the
   exchange endpoint as safe from replay until framework recommendation 2 lands; the tenant allow-list
   and a subject-based resolver (both already in place here) are necessary compensating controls, not
   sufficient ones on their own.
2. **Do not adopt the redirect/BFF external-auth entry point** until framework recommendation 1 lands.
   PlatformSubscriptions already does not use it.
3. **Decide the acceptable exposure window for a compromised account** between "the user acts" (reset,
   sign-out-everywhere) and the moment their still-live access token naturally expires. Today that
   window is exactly `AuthTokens:AccessTokenMinutes` (15 minutes by default) in every case measured
   here. Shorten it if that is not acceptable for this platform's threat model; there is no other lever
   available before framework recommendation 9 is decided.
4. **Verify nothing in the client UI depends on `BlazorBaseUserAuthStateProvider` reporting
   "authenticated"** under the bearer transport, given `CLIENT-401-INVALIDATES`. Server-side
   authorization is unaffected either way.
5. **Keep `PlatformExternalUserResolver` subject-based** if it is ever revisited — it is the reason
   this repository is not exposed to `EXT-UNVERIFIED-EMAIL-TAKEOVER`'s shape today.

---

## Where the evidence lives

- Full probe-by-probe run, including request/response evidence for every finding above:
  `dotnet run --project Libs/BlazorBase.User.SecurityHarness -- --profile all --format markdown`.
- CI wrapper and the accepted-findings baseline:
  [`BlazorBase.User.SecurityHarness.Test`](../BlazorBase.User.SecurityHarness.Test) and
  [`Baseline/known-findings.baseline`](../BlazorBase.User.SecurityHarness/Baseline/known-findings.baseline).
- Probe catalogue and harness design: [`BlazorBase.User.SecurityHarness/README.md`](../BlazorBase.User.SecurityHarness/README.md).
