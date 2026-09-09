# BlazorBase.User.SecurityHarness

An offensive security test harness for the BlazorBase2 authentication stack
(`BlazorBase.User.Server`). It starts real Kestrel hosts - not `TestServer` - for every
authentication configuration the framework supports, and runs a catalogue of adversarial probes
against them.

**This is an attack tool. Only point it at hosts you own and are authorised to test.** Every host
it starts is its own, in-process, loopback-only Kestrel instance; it never talks to the real
Microsoft identity platform or any production system unless `--base-url` is pointed at one on
purpose.

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
| `External` | `AddBlazorBaseExternalAuth` pointed at the fake identity provider started alongside every run (see "Fake identity provider" below). |
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
