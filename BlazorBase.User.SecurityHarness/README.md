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
| `External` | `AddBlazorBaseExternalAuth` pointed at the fake identity provider added in step 2. Reports as unavailable until then. |
| `AccessTokens` | `AddBlazorBaseAccessTokens` with defaults. |
| `DevelopmentLogin` | `AddBlazorBaseDevelopmentAuthentication` in the Development environment. |
| `ProductionWithDevelopmentLogin` | The same registration with `EnvironmentName = "Production"`. |

`CookieWithoutCsrfMiddleware` and `ProductionWithDevelopmentLogin` are expected to fail: the
framework's own startup guards exist precisely to catch these two misconfigurations. The report
records the guard's exact message rather than treating the failure as a harness error.

The self-signed certificate is generated in-process for every run and never touches a machine
certificate store. A probe's HTTP client trusts exactly that certificate's thumbprint - never a
blanket `_ => true` validation callback.

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
