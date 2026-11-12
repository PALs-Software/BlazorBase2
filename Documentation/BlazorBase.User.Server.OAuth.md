# OAuth 2.1 authorization server for MCP endpoints

`BlazorBase.User.Server` can act as a minimal OAuth 2.1 authorization server, so an endpoint protected by
the [access-token scheme](BlazorBase.User.Server.md#access-tokens-machine-to-machine) - typically an MCP
endpoint - can be added as a **claude.ai custom connector** (web, desktop, mobile) or used by any other
OAuth client. The user signs in with their normal site account and confirms the connection on a
consent page; the client receives an ordinary access token.

It is opt-in per host. A host that does not call `AddBlazorBaseOAuthServer` gets no endpoints and no
behaviour change - only empty tables in its next migration and the plain `WWW-Authenticate: Bearer`
header described under [Upgrade notes](#upgrade-notes).

---

## Table of Contents

- [Why, and what it is not](#why-and-what-it-is-not)
- [The flow](#the-flow)
- [Registration](#registration)
- [Configuration: OAuthServer](#configuration-oauthserver)
- [Client ID Metadata Documents](#client-id-metadata-documents)
- [Endpoint reference](#endpoint-reference)
- [Consent page and connected apps](#consent-page-and-connected-apps)
- [Why the endpoints live under api/](#why-the-endpoints-live-under-api)
- [CSRF](#csrf)
- [Security notes](#security-notes)
- [Setting up a claude.ai connector](#setting-up-a-claudeai-connector)
- [Data model and migration](#data-model-and-migration)
- [Upgrade notes](#upgrade-notes)
- [Limitations](#limitations)

---

## Why, and what it is not

claude.ai custom connectors are OAuth-based. They discover the authorization server through RFC 9728
protected-resource metadata, identify themselves with an OAuth Client ID Metadata Document (the
registration the MCP specification recommends; dynamic client registration, RFC 7591, is the deprecated
fallback that is supported as well), run the authorization-code flow with PKCE in the user's browser and
call the resource with the issued bearer token. A static `Authorization` header - which is all Claude
Code needs - is not an option there.

The OAuth access token **is an `AccessToken` row** with `OAuthGrantId` set. The existing
`AccessTokenAuthenticationHandler`, the claims augmentors (owner roles) and the immediate revocation
semantics apply unchanged, and the protected endpoint keeps
`RequireAuthorization(AccessTokenAuthenticationDefaults.AuthenticationScheme)`. Nothing is specific to an
MCP SDK.

Deliberately not included:

- **No OpenID Connect** - no `id_token`, no `userinfo`.
- **No implicit, password or client-credentials grant**, and no JWT access tokens.
- **No scope enforcement.** Scopes are advertised and recorded on the grant (configurable, default `mcp`)
  but not turned into claims: an OAuth token acts with its owner's rights, exactly like a personal access
  token. An endpoint that needs more restriction authorizes on roles or on the grant itself.
- **No OpenIddict and no MCP SDK dependency.** OpenIddict has neither Client ID Metadata Documents nor
  dynamic client registration, and would
  bring its own token format, validation handler and four tables; the access-token stack already does
  validation.

---

## The flow

```
claude.ai (backend)                       BlazorBase host                         User's browser
-------------------                       ---------------                         --------------
POST /api/mcp (no token)        ───────►  401 WWW-Authenticate: Bearer
                                          resource_metadata="https://host/.well-known/oauth-protected-resource/api/mcp"
GET  /.well-known/oauth-protected-resource/api/mcp
                                ───────►  { resource, authorization_servers: [issuer], scopes_supported }
GET  /.well-known/oauth-authorization-server
                                ───────►  { authorization_endpoint, token_endpoint, registration_endpoint, ... }
POST /api/oauth/register        ───────►  201 { client_id, ... }        (RFC 7591)

                                          GET /api/oauth/authorize?client_id&redirect_uri      ◄── browser
                                              &code_challenge&code_challenge_method=S256&state&scope&resource
                                          302 /oauth/consent?request=<sealed, 10 min>          ──► browser
                                          (not signed in: RedirectToLogin → /login?returnUrl=... → back)
                                          GET  /api/oauth/authorization-requests?request=...   ◄── consent page
                                          POST /api/oauth/authorization-requests/decision      ◄── "Allow"
                                          { redirectUri: "<redirect_uri>?code&state&iss" }     ──► browser
                                                                                                    │
GET <redirect_uri>?code=...&state=...&iss=...   ◄───────────────────────────────────────────────────┘
POST /api/oauth/token  grant_type=authorization_code&code&code_verifier&redirect_uri&resource
                                ───────►  { access_token, token_type: Bearer, expires_in, refresh_token, scope }
POST /api/mcp  Authorization: Bearer <access_token>
                                ───────►  200 (AccessTokenAuthenticationHandler, as for any access token)
POST /api/oauth/token  grant_type=refresh_token&refresh_token
                                ───────►  new pair (rotation; the old refresh token is consumed)
```

With a Client ID Metadata Document the `register` call is skipped: Claude sends `client_id=<https URL of
its document>` to `authorize`, and the server fetches and caches that document (see
[Client ID Metadata Documents](#client-id-metadata-documents)). Claude takes that path whenever the
metadata advertises `client_id_metadata_document_supported: true` and `none` in
`token_endpoint_auth_methods_supported`, both of which this server does by default.

---

## Registration

```csharp
using BlazorBase.User.Server.AccessTokens;
using BlazorBase.User.Server.OAuth;
using BlazorBase.User.Server.OAuth.Endpoints;

builder.Services.AddBlazorBaseUserServer<AppUser, AppDbContext>(builder.Configuration);
builder.Services.AddBlazorBaseAccessTokens<AppDbContext>(builder.Configuration);      // prerequisite
builder.Services.AddAccessTokenOwnerRoleClaims<AppUser>();                             // when the endpoint authorizes on roles
builder.Services.AddBlazorBaseOAuthServer<AppUser, AppDbContext>(builder.Configuration); // section "OAuthServer"

var app = builder.Build();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseBlazorBaseAccessTokenStampValidation();
app.UseBlazorBaseCsrf();                      // cookie transport only
app.UseAuthorization();

app.MapBlazorBaseAuthSessionEndpoint();
app.MapBlazorBaseOAuthServer();                       // metadata, register, authorize, token, revoke, consent + grants API
app.MapBlazorBaseOAuthProtectedResource("/api/mcp");  // once per protected endpoint

app.MapMcp("/api/mcp")
   .AllowBlazorBaseCsrf()                             // bearer-only endpoint; see CSRF below
   .RequireAuthorization(policy => policy
       .AddAuthenticationSchemes(AccessTokenAuthenticationDefaults.AuthenticationScheme)
       .RequireAuthenticatedUser());

app.MapFallbackToFile("index.html");
```

- `AddBlazorBaseOAuthServer<TUser, TContext>(configuration, configureOptions?)` binds `OAuthServerOptions`
  from the `OAuthServer` section (then `configureOptions`), validates them at startup
  (`OAuthServerOptionsValidator`, `ValidateOnStart`), and registers `IOAuthClientService`,
  `IOAuthGrantService`, the flow handlers, the RFC 9728 challenge hook and `OAuthCleanupService<TUser>`.
- **`AddBlazorBaseAccessTokens` is required.** Without it the host fails while the request pipeline is
  built (`OAuthServerStartupGuardFilter`), with a message naming the missing call. The two registrations
  may be made in either order.
- `AddBlazorBaseUserServer` is required as well: it provides Identity, the `"BlazorBaseAuth"` rate-limit
  policy (enforced only with `app.UseRateLimiter()`) and the default authentication scheme the consent API
  uses.
- `MapBlazorBaseOAuthProtectedResource(path)` publishes the resource's metadata and registers the path in
  the singleton `OAuthProtectedResourceRegistry`; the endpoint itself keeps its own `RequireAuthorization`.
  The first registered path also answers the bare `/.well-known/oauth-protected-resource`.
- Both `Map...` calls throw `InvalidOperationException` when `AddBlazorBaseOAuthServer` was not called.

The client side - the consent page, the connected-apps panel and `IOAuthConsentService` - is described in
[BlazorBase.User](BlazorBase.User.md#oauth-consent-page-and-connected-apps).

---

## Configuration: OAuthServer

```json
"OAuthServer": {
  "IssuerUri": "https://app.example.com",
  "EndpointBasePath": "api/oauth",
  "ConsentPagePath": "/oauth/consent",
  "Scopes": { "mcp": "Use this application's tools and data with your account." },
  "RequiredRoles": [ "Admin", "User" ],
  "EnableDynamicClientRegistration": true,
  "EnableClientIdMetadataDocuments": true,
  "AllowedClientIdMetadataDocumentHosts": [ "claude.ai", "claude.com" ],
  "ClientIdMetadataDocumentMinimumCacheDuration": "00:05:00",
  "ClientIdMetadataDocumentMaximumCacheDuration": "1.00:00:00",
  "ClientIdMetadataDocumentFetchTimeout": "00:00:05",
  "AllowedRedirectUriHosts": [ "claude.ai", "claude.com" ],
  "AdditionalRedirectUriSchemes": [],
  "AccessTokenLifetime": "01:00:00",
  "RefreshTokenLifetime": "30.00:00:00",
  "AuthorizationCodeLifetime": "00:02:00",
  "AuthorizationRequestLifetime": "00:10:00",
  "RefreshTokenReuseGracePeriod": "00:00:30",
  "ResourceName": "My App MCP",
  "CleanupInterval": "06:00:00"
}
```

| Option | Default | Meaning |
|---|---|---|
| `IssuerUri` | derived | Issuer and base of every advertised URL. Absolute `https`, or `http` on `localhost`/`127.0.0.1`/`[::1]`; no query or fragment. Empty: `{scheme}://{host}{pathBase}` of the request - behind a reverse proxy only correct with `AddBlazorBaseForwardedHeaders` + `app.UseForwardedHeaders()`, so set it explicitly in production. |
| `EndpointBasePath` | `api/oauth` | Base of the flow, consent and grants endpoints. Keep it under `api/` (see below). Must match `BlazorBaseUserClientOptions.OAuthEndpointBasePath` on the client. |
| `ConsentPagePath` | `/oauth/consent` | The client route `authorize` redirects to; a local path. |
| `Scopes` | `mcp` | Name → description. Advertised and recorded only. Empty means the single `mcp` scope. |
| `RequiredRoles` | empty | The approving user must hold one of these roles; empty allows any signed-in user. |
| `EnableDynamicClientRegistration` | `true` | Maps `register`. When `false`, the endpoint is not mapped and not advertised; the host creates clients through `IOAuthClientService.RegisterAsync` (same validation). |
| `EnableClientIdMetadataDocuments` | `true` | Accepts an `https` URL as `client_id` and loads the client from the document there; advertised as `client_id_metadata_document_supported`. When `false`, such a client id is unknown. |
| `AllowedClientIdMetadataDocumentHosts` | empty | When set, documents are fetched only from these hosts (case-insensitive) - recommended: `claude.ai`, `claude.com`. Empty allows any host that resolves to a public address. |
| `ClientIdMetadataDocumentMinimumCacheDuration` | 5 min | A fetched document is reused at least this long, whatever its `Cache-Control` says (also after `no-store`/`no-cache`, and as the wait after a failed fetch). |
| `ClientIdMetadataDocumentMaximumCacheDuration` | 24 h | A fetched document is reused at most this long; must not be below the minimum. |
| `ClientIdMetadataDocumentFetchTimeout` | 5 s | Time limit of one fetch. |
| `MaximumUnusedClientIdMetadataDocumentClients` | 1000 | While this many metadata document clients have no grant, a new client id is refused (the cleanup service removes unused ones after a day), so anonymous `authorize` requests cannot fill the client table. |
| `AllowedRedirectUriHosts` | empty | When set, every redirect URI's host must be in it (case-insensitive) - recommended: `claude.ai`, `claude.com`. Applies to registered clients and to the `redirect_uris` of metadata documents. |
| `AdditionalRedirectUriSchemes` | empty | Private-use schemes (`com.example.app`) accepted besides `https` and loopback `http`. `http`, `https`, `javascript`, `data`, `vbscript`, `file`, `blob` and `about` are refused. |
| `AccessTokenLifetime` | 1 h | Lifetime of each issued access token (`expires_in`). |
| `RefreshTokenLifetime` | 30 d | Lifetime of each refresh token. Every refresh issues a new one. |
| `AuthorizationCodeLifetime` | 2 min | Lifetime of an authorization code. |
| `AuthorizationRequestLifetime` | 10 min | How long the consent page may take. |
| `RefreshTokenReuseGracePeriod` | 30 s | A consumed refresh token presented again within this window is a plain `invalid_grant` (a client retrying after a lost response); after it, the whole grant is revoked (theft detection). |
| `ResourceName` | none | Published as `resource_name` in the protected-resource metadata. |
| `CleanupInterval` | 6 h | Interval of `OAuthCleanupService<TUser>`. |

---

## Client ID Metadata Documents

Implements draft-ietf-oauth-client-id-metadata-document (revision -03) as the MCP specification
2026-07-28 requires. A client id that starts with `https://` is never a registered id (those start with
`bbc_`); `IOAuthClientService.ResolveAsync` hands it to `OAuthClientIdMetadataDocumentResolver<TUser>`.

**The URL** must be absolute `https`, at most 400 characters, with a path other than `/`, and without user
information, query, fragment, dot segments (also `%2e`) or whitespace; with
`AllowedClientIdMetadataDocumentHosts` set, its host must be listed. Anything else is refused before a
request is made.

**The fetch** (`OAuthClientIdMetadataDocumentFetcher`, named `HttpClient`
`BlazorBase.OAuth.ClientIdMetadataDocuments`) sends `GET` over HTTP/1.1 or lower (HTTP/3 would bypass the
connect callback) with `Accept: application/json`, a `User-Agent`, no cookies and no proxy, follows **no
redirects**, and connects only after resolving the host itself and checking every resolved address
against the special-use ranges of RFC 6890 (`OAuthSpecialUseAddresses`: loopback, private, link-local,
shared, documentation, multicast, reserved, IPv4-mapped and IPv4-compatible forms included). Only a `200`
with an `application/json` or `+json` content type and at most 5 KB of body counts. Network failures are
answered with a generic message; the detail is logged only, so the endpoint reveals nothing about how
internal host names resolve.

**The document** must have `client_id` equal to the URL (ordinal), at least one entry in `redirect_uris`
(1-10, same rules as for `register`, including `AllowedRedirectUriHosts`), `grant_types` that include
`authorization_code` (default `authorization_code`; unsupported values are dropped, so `refresh_token` must
be listed to get refresh tokens), and `response_types` that include `code` (default `code`). It must not
carry `client_secret`/`client_secret_expires_at` or both `jwks` and `jwks_uri`. Token endpoint
authentication: `token_endpoint_auth_methods_supported` wins over `token_endpoint_auth_method`, which
must then be one of its values; neither means `none`. The list must contain `none` and no shared-secret
method - these clients are public clients, `private_key_jwt` is not supported. `client_name` (at most 200
characters) defaults to the URL's host.

**Caching.** The valid document is stored as an `OAuthClient` with `IsMetadataDocumentClient = true` and
`MetadataDocumentExpiresAt = now + max-age`, clamped to the minimum and maximum cache duration
(`no-store`/`no-cache` and a missing header mean the minimum; `Expires` is ignored). Grants reference that
row like any client's. An expired row is fetched again on the next `authorize`, consent or token request.
A client id that matches a stored row only case-insensitively (SQL Server's default collation compares
that way, URL paths do not) is refused before anything is fetched, so a case variant can neither borrow a
cached client nor trigger the revocation of its grants.

**Failures.** At `authorize`, a refused URL or document is a plain 400 naming the reason, never a redirect.
A fetch that fails transiently (network, timeout, a connection dropped while reading, `408`, `429`, `5xx`)
keeps an already cached client usable - for at most twice `ClientIdMetadataDocumentMaximumCacheDuration`
after its last successful fetch - and waits the minimum cache duration before trying again; without a
usable cached client it is a 400 (or `invalid_client` at the token endpoint). The consent decision checks
again that the redirect URI is still listed by the (possibly refetched) document. A fetched document that is refused - its `client_id` no longer
matches, or it now breaks a rule - **revokes every grant of the cached client**, as the draft recommends
for a document that can no longer be trusted. A refused fetch (`404`, a redirect, wrong content type,
too large, malformed JSON) only fails the request.

**Consent.** The consent page shows the host of the client id ("The app is identified by: claude.ai") in
addition to the self-asserted name and the redirect host; unlike the name, that host cannot be chosen by
whoever starts the request.

**Loopback redirect URIs** match with any port (RFC 8252 section 7.3), for metadata document clients and
registered clients alike: `http://localhost/callback` registered, `http://localhost:3118/callback` requested
is accepted (Claude Code listens on an ephemeral port). Only `http` on `localhost`, `127.0.0.1` or `[::1]`
is treated this way, and the comparison works on the raw strings: only the `:port` is cut out, everything
else must be ordinally equal, so a URI that would match only after normalization (dot segments, escapes,
case, whitespace) is refused, and the browser is sent to a URI that differs from the registered one in
nothing but the port.

---

## Endpoint reference

All paths are relative to the host's `PathBase`; `{base}` is `EndpointBasePath`.

| Method | Path | Purpose | Auth | CSRF |
|---|---|---|---|---|
| GET | `/.well-known/oauth-authorization-server` | RFC 8414 metadata | anonymous | n/a |
| GET | `/.well-known/oauth-protected-resource{path}` and bare `/.well-known/oauth-protected-resource` | RFC 9728 metadata (bare = first registered resource, 404 when none) | anonymous | n/a |
| POST | `{base}/register` | RFC 7591 dynamic client registration (JSON) | anonymous, rate-limited | opted out |
| GET | `{base}/authorize` | validates, redirects to the consent page | anonymous, rate-limited | n/a |
| POST | `{base}/token` | `authorization_code`, `refresh_token` (form) | client auth, rate-limited | opted out |
| POST | `{base}/revoke` | RFC 7009 revocation (form) | client auth, rate-limited | opted out |
| GET | `{base}/authorization-requests?request=…` | what the consent page shows | signed-in user | n/a |
| POST | `{base}/authorization-requests/decision` | `{ request, approved }` → `{ redirectUri }` | signed-in user | **enforced** |
| GET | `{base}/grants` | the current user's connected apps | signed-in user | n/a |
| DELETE | `{base}/grants/{id}` | revoke one of the current user's grants | signed-in user | enforced |

The metadata endpoints answer with `Access-Control-Allow-Origin: *` (and a matching `OPTIONS` preflight):
the documents are public, and browser-based MCP clients and inspectors read them cross-origin. Every
`{base}/*` response carries `Cache-Control: no-store` and `Pragma: no-cache`. Errors use the RFC 6749 JSON
shape `{ "error": "...", "error_description": "..." }`: 400, or 401 for `invalid_client` - with
`WWW-Authenticate: Basic` when the client authenticated with HTTP Basic.

### Authorization-server metadata

`issuer`, `authorization_endpoint`, `token_endpoint`, `registration_endpoint` (only with dynamic
registration), `revocation_endpoint`, `response_types_supported: ["code"]`,
`grant_types_supported: ["authorization_code","refresh_token"]`, `code_challenge_methods_supported: ["S256"]`,
`token_endpoint_auth_methods_supported` and `revocation_endpoint_auth_methods_supported`
(`none`, `client_secret_post`, `client_secret_basic`), `scopes_supported`,
`authorization_response_iss_parameter_supported: true`, and `client_id_metadata_document_supported: true`
while `EnableClientIdMetadataDocuments` is on.

### Protected-resource metadata and the challenge

`resource` (`{issuer}{path}`), `authorization_servers: [issuer]`, `scopes_supported`,
`bearer_methods_supported: ["header"]`, `resource_name` (when configured).

`AccessTokenAuthenticationHandler` answers an unauthenticated request with `401` and
`WWW-Authenticate: Bearer`: with `error="invalid_token"` when a token was presented and rejected, and
with `resource_metadata="{issuer}/.well-known/oauth-protected-resource{path}"` when the request path is
at or below a registered resource (segment-boundary prefix match: `/api/mcp` covers `/api/mcp/sse`, not
`/api/mcpx`). The parameters come from `IAccessTokenChallengeParameterProvider` implementations; the
OAuth module registers `OAuthProtectedResourceChallengeParameterProvider`, and a host can add its own.

### `register`

Request (JSON; unknown members are ignored):

| Member | Rule |
|---|---|
| `redirect_uris` | Required, 1-10 entries. Each absolute, no fragment, no user information, at most 2000 characters; `https`, or `http` on a loopback host, or a scheme from `AdditionalRedirectUriSchemes`; host in `AllowedRedirectUriHosts` when that is set. Violation: `invalid_redirect_uri`. |
| `client_name` | Optional, at most 200 characters; default `Unnamed client`. |
| `token_endpoint_auth_method` | `none`, `client_secret_post` or `client_secret_basic` (default, per RFC 7591). |
| `grant_types` | Subset of `authorization_code`, `refresh_token`; must contain `authorization_code`; default `authorization_code`. A refresh token is only ever issued to a client that registered `refresh_token`. |
| `response_types` | Only `code` (default). |

Any other violation, or a body that is not a JSON object, is `invalid_client_metadata`. Response `201`:
`client_id` (`bbc_` + 22 Base64Url characters), `client_id_issued_at`, `client_name`, `redirect_uris`,
`grant_types`, `response_types`, `token_endpoint_auth_method`, and for a secret method `client_secret`
(32 random bytes, Base64Url; only its SHA-256 hash is stored) with `client_secret_expires_at: 0`.

### `authorize`

1. Unknown or missing `client_id`, a client id metadata document that cannot be fetched or is refused, a
   `redirect_uri` that is not an exact (ordinal) match of a registered URI - a loopback `http` URI may
   differ in its port - or no `redirect_uri` while the client registered more than one → **400 plain
   text, no redirect**.
2. Everything else is reported to the redirect URI with `error`, `error_description`, `state` and `iss`
   (RFC 9207): `response_type` other than `code` → `unsupported_response_type`; missing `code_challenge`,
   `code_challenge_method` other than `S256` (a missing method means `plain` and is refused), or a
   challenge that is not 43-128 Base64Url characters → `invalid_request`; a `scope` outside the configured
   scopes → `invalid_scope` (no scope means all configured scopes); a `resource` that is not a registered
   protected resource → `invalid_target` (compared as absolute URIs, case-insensitive scheme and host,
   trailing slash ignored); a repeated parameter → `invalid_request`.
3. Success: the request is sealed with an `ITimeLimitedDataProtector` (purpose
   `BlazorBase.User.Server.OAuth.AuthorizationRequest.v1`, lifetime `AuthorizationRequestLifetime`) and
   the browser is sent `302` to `{PathBase}{ConsentPagePath}?request=<sealed>`. The server keeps no state
   for a request the user never answers.

### `authorization-requests` and `decision`

`GET` unseals the request (invalid or expired → 400) and returns
`{ clientName, redirectHost, clientIdHost, scopes: [{ name, description }] }` (`clientIdHost` only for a
metadata document client). `POST decision` with
`{ request, approved }` unseals it again (400), requires `ClaimTypes.NameIdentifier` of a session
principal (a principal authenticated by an access token is refused) and, when `RequiredRoles` is set, one
of those roles (403 - also on `GET`). Denied → `{ redirectUri: "<redirect_uri>?error=access_denied&state&iss" }`.
Approved → an `OAuthGrant` (client, user, scope, resource, redirect URI) and a single-use code (32 random
bytes, SHA-256 hash stored, lifetime `AuthorizationCodeLifetime`); response
`{ redirectUri: "<redirect_uri>?code&state&iss" }`. Parameters are appended to a query the client
registered, never replacing it.

### `token`

Client authentication follows the method the client registered: `none` sends only `client_id` (a secret
is refused), `client_secret_post` sends `client_id` + `client_secret` as form fields, `client_secret_basic`
sends them form-urlencoded in a Basic header. Secrets are compared by hash in constant time; a mismatch
or a method other than the registered one is `invalid_client`.

`grant_type=authorization_code` - `code`, `code_verifier`, `redirect_uri`, optional `resource`:

| Condition | Answer |
|---|---|
| unknown code, grant revoked, or code issued to another client | `invalid_grant` |
| code already redeemed | `invalid_grant` **and the grant is revoked** (RFC 6749 §4.1.2) |
| code expired | `invalid_grant` |
| `redirect_uri` differs from the one the authorization request named (or, when it named none, from the grant's) | `invalid_grant` |
| `code_verifier` not 43-128 unreserved characters, or `BASE64URL(SHA256(verifier))` ≠ challenge (constant time) | `invalid_grant` |
| `resource` differs from the grant's | `invalid_target` |
| owner inactive or deleted | `invalid_grant` |

The code is consumed by a conditional update (`WHERE ConsumedAt IS NULL`); a parallel redemption that
loses the race is treated as a replay.

`grant_type=refresh_token` - `refresh_token`, optional `scope` (must be within the grant's) and `resource`:
unknown token, revoked grant, another client's grant, expired token or inactive owner → `invalid_grant`;
a client not registered for `refresh_token` → `unauthorized_client`; a consumed token within
`RefreshTokenReuseGracePeriod` → `invalid_grant`, after it → the grant is revoked, then `invalid_grant`.
Otherwise the token is consumed (conditional update) and a new pair is issued.

Issued access token: `IAccessTokenService.CreateAsync<AccessToken>("OAuth: {client name}" (cut to 100
characters), now + AccessTokenLifetime, token => token.OAuthGrantId = grant.Id, grant.UserId)`. Response
`{ access_token, token_type: "Bearer", expires_in, refresh_token, scope }`; `refresh_token` only for a
client registered for that grant. `grant.LastUsedAt` and `client.LastUsedAt` are updated. Any other
`grant_type` is `unsupported_grant_type`.

### `revoke`

Client authentication as above; form `token` (`token_type_hint` is ignored). A refresh token of the
client's own grant revokes the whole grant; an access token issued under one of the client's grants is
revoked on its own. Unknown tokens and other clients' tokens are answered `200` as RFC 7009 requires.

### `grants`

`GET` returns the current user's grants that are not revoked: `{ id, clientName, redirectHost, scope,
createdAt, lastUsedAt }`, newest first. `DELETE grants/{id}` revokes one (`204`; `404` when it is not the
caller's or already revoked). Revoking a grant sets `RevokedAt`, consumes all its refresh tokens, sets
`IsRevoked` on every `AccessToken` carrying its id (one bulk update) and evicts those ids from
`IAccessTokenCache` - the tokens stop authenticating on the very next request, on every instance, because
access-token validation always re-reads the row. `IOAuthGrantService` exposes the same `ListAsync` and
`RevokeAsync` to host code.

---

## Consent page and connected apps

`BlazorBase.User` ships the page `/oauth/consent` (`OAuthConsent`, `[Authorize]`) and the component
`OAuthConnectedAppsPanel`, both localized in English and German, and the client service
`IOAuthConsentService` they use. An unauthenticated visitor of the consent page is sent through the host's
`RedirectToLogin` to `/login?returnUrl=/oauth/consent?request=...` and back after signing in. Registration
and usage are described in [BlazorBase.User](BlazorBase.User.md#oauth-consent-page-and-connected-apps).

---

## Why the endpoints live under api/

A BlazorBase PWA's service worker answers every navigation request with the cached `index.html` except
paths under `/api/`. The browser reaches `authorize` by a top-level navigation; at `/oauth/authorize` the
service worker would answer with the app shell and the server would never see the request. Under
`/api/oauth/authorize` it passes through. The consent page itself is a client route
(`/oauth/consent`), so being served from `index.html` is exactly what it needs. If you change
`EndpointBasePath`, keep it below a prefix the service worker does not intercept. The `/.well-known/*`
documents are fetched by clients, not navigated to, and are unaffected.

---

## CSRF

`register`, `token` and `revoke` are called server-to-server, carry their own client authentication and
cannot ride on a user's cookie, so they carry `AllowBlazorBaseCsrf` - a public client posts to `token`
without any `Authorization` header and would otherwise be refused by the antiforgery check. The consent
`decision` and `DELETE grants/{id}` are authenticated by the session cookie under the cookie transport
and keep the CSRF enforcement; the BlazorBase client sends the antiforgery header through
`CsrfHeaderHandler` (cookie transport) or a bearer header (bearer transport) as for any other API call.

The protected MCP endpoint should carry `AllowBlazorBaseCsrf()` under the cookie transport: an MCP client
authenticates with a bearer header, and its very first, unauthenticated probe must receive the `401`
challenge with `resource_metadata` rather than a `403` from the antiforgery check.

---

## Security notes

- **No open redirector.** Redirect URIs are judged once, at registration or when a metadata document is
  fetched; `authorize` only redirects to a URI the client listed, compared by exact string (loopback
  `http` ports aside). Until client and redirect URI are established, errors are a plain 400. Set
  `AllowedRedirectUriHosts` to `claude.ai`/`claude.com` when only claude.ai connectors are expected.
- **No server-side request forgery through client ids.** A metadata document is fetched only over
  `https`, without redirects or proxy, from a host whose resolved addresses are all public; set
  `AllowedClientIdMetadataDocumentHosts` to restrict it further. The check runs on the addresses the
  socket connects to, so DNS rebinding does not get past it.
- **Every code requires an explicit decision.** No consent is remembered; each authorization goes
  through the consent page.
- **The client name is self-asserted.** Anyone can register a client called "Claude". The consent page
  therefore shows the redirect host next to it, and the user is told to allow only a connection they
  started.
- **Nothing secret is stored in plain text.** Client secrets, codes and refresh tokens are stored as
  SHA-256 hashes (high-entropy random values, so no salt is needed); access tokens as SHA-512 hashes by
  the access-token service. Comparisons are constant-time.
- **PKCE `S256` is mandatory**, `plain` is refused. Codes are single-use and short-lived; replay revokes
  the grant. Refresh tokens rotate with reuse detection.
- **Do not let the consent page be framed.** A page that another site can embed can be clickjacked into
  an *Allow*. The consent page is a client route served from `index.html`, so the framework cannot set a
  per-page header; the host should send `Content-Security-Policy: frame-ancestors 'none'` (or
  `X-Frame-Options: DENY`) on its responses.
- **An access token cannot approve a new client.** The consent API refuses a principal authenticated by
  the access-token scheme.
- **Rate limiting** of `register`, `authorize`, `token` and `revoke` uses the `"BlazorBaseAuth"` policy
  (default 10 requests per minute per client IP, `AuthTokens:RateLimit:*`). claude.ai calls `token` from
  its own servers, so several users of one site share that bucket; raise `PermitLimit` if many users
  connect at once. Behind a reverse proxy configure forwarded headers, or every client shares one bucket.
- **Issuer.** Set `IssuerUri` in production. A request-derived issuer is only as trustworthy as the
  `Host` header and the forwarded scheme.
- **Owner checks.** Deactivating or deleting the owner rejects their OAuth access tokens at once (the
  access-token validation checks the owner) and refuses further refreshes.

---

## Setting up a claude.ai connector

1. Register the server side as above, map the MCP endpoint as protected resource, and deploy over HTTPS
   with `OAuthServer:IssuerUri` set to the public origin (for example `https://app.example.com`).
2. Register `IOAuthConsentService` on the client and place `OAuthConnectedAppsPanel` on a settings page.
3. Add the migration (below).
4. In claude.ai: *Settings → Connectors → Add custom connector*, URL = the protected resource, for
   example `https://app.example.com/api/mcp`. Keep the default OAuth client option (Claude's published
   identity): Claude then uses its Client ID Metadata Document. The automatic registration option uses
   `register` instead; both work. Never enter a client id or secret.
5. *Connect* opens the consent page in the browser; after *Allow* the connector is ready. The user can
   disconnect it at any time in the connected-apps panel.

---

## Data model and migration

Declared on `BaseUserDbContext<TUser>`, so every deriving context carries the tables:

| Entity | Columns |
|---|---|
| `OAuthClient` (`OAuthClients`) | `Id` (key, max 400 - a metadata document URL for those clients), `ClientName` (max 200), `RedirectUris` and `GrantTypes` (primitive collections, JSON columns), `TokenEndpointAuthMethod` (max 32), `ClientSecretHash` (max 128, nullable), `CreatedAt`, `LastUsedAt`, `IsMetadataDocumentClient`, `MetadataDocumentExpiresAt` and `MetadataDocumentFetchedAt` (nullable) |
| `OAuthGrant` (`OAuthGrants`) | `Id`, `ClientId` (max 400, FK, cascade), `UserId` (FK to the user, cascade), `Scope` (max 1000), `Resource` (max 2000), `RedirectUri` (max 2000), `CreatedAt`, `LastUsedAt`, `RevokedAt`; indexes on `UserId`, `ClientId` |
| `OAuthAuthorizationCode` (`OAuthAuthorizationCodes`) | `Id`, `CodeHash` (unique), `GrantId` (FK, cascade), `RedirectUri` (as named in the request, nullable), `CodeChallenge`, `ExpiresAt`, `ConsumedAt` |
| `OAuthRefreshToken` (`OAuthRefreshTokens`) | `Id`, `TokenHash` (unique), `GrantId` (FK, cascade), `CreatedAt`, `ExpiresAt`, `ConsumedAt` |
| `AccessToken` | new nullable `OAuthGrantId` with an index; deliberately no foreign key |

**Every deriving application needs a migration**, even one that never calls `AddBlazorBaseOAuthServer`
(it then only gets empty tables):

```bash
dotnet ef migrations add AddOAuthServer
```

`OAuthCleanupService<TUser>` runs every `CleanupInterval` and deletes codes expired more than a day ago,
refresh tokens expired or consumed more than a day ago, OAuth access tokens expired more than a day ago,
grants revoked more than 30 days ago (with their codes and refresh tokens) and clients older than a day
that never obtained a grant (abandoned registrations, and cached metadata documents nobody connected
with - they are fetched again on demand).

---

## Upgrade notes

- **`WWW-Authenticate` on access-token 401s.** `AccessTokenAuthenticationHandler` now answers a challenge
  with `WWW-Authenticate: Bearer` (plus `error="invalid_token"` when a presented token was rejected).
  Before, the 401 carried no header. Without the OAuth module that is the only change.
- **`IAccessTokenService.ListAsync` excludes OAuth tokens** (`OAuthGrantId` set). They are managed through
  their grant, not as personal tokens.
- **Migration:** the `AccessTokens.OAuthGrantId` column and the four OAuth tables, see above.

---

## Limitations

- No scope enforcement and no fine-grained permissions: the token acts with its owner's rights.
- No OpenID Connect, no JWT access tokens, no token introspection endpoint.
- No remembered consent: each new connection asks again.
- A host mounted below a `PathBase` publishes its metadata below that path base
  (`https://host/base/.well-known/...`), not at the RFC 8414/9728 path-insertion location
  (`https://host/.well-known/oauth-authorization-server/base`). The `resource_metadata` URL in the
  challenge points at the right place, but clients that guess the authorization-server metadata URL from
  the issuer may not find it; host the application at the root of its origin when in doubt.
