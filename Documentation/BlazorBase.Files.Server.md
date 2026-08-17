# BlazorBase.Files.Server

Server-side ASP.NET Core library (net10.0, `Microsoft.AspNetCore.App`) that provides filesystem
storage, image thumbnail generation, a signed-token access gate, an abstract file controller base,
EF Core entity configuration, and a temporary file cleanup hosted service. It is the server
companion to the browser-safe **BlazorBase.Files** RCL.

---

## Overview

| Concern | Type |
|---|---|
| Filesystem storage | `BlazorBase.Files.Server.Services.IFileStorage` / `FileSystemFileStorage` |
| Image/thumbnail processing | `BlazorBase.Files.Server.Services.IImageService` / `ImageSharpImageService` |
| File access authorization hook | `BlazorBase.Files.Server.Services.IFileAccessAuthorizer` / `OwnerScopedFileAccessAuthorizer` (default) |
| Signed-token service | `BlazorBase.Files.Server.Services.IFileAccessTokenService` / `HmacFileAccessTokenService` |
| Token options | `BlazorBase.Files.Server.Services.FileAccessTokenOptions` |
| Abstract REST controller | `BlazorBase.Files.Server.Controllers.BaseFileControllerBase` |
| EF Core model extension | `BlazorBase.Files.Server.Data.BaseFileEntityConfiguration.ApplyBaseFileConfiguration` |
| DI registration | `AddBlazorBaseFilesServer` / `AddBlazorBaseFileAccessAuthorizer<T>` |
| Temp-file cleanup | `BlazorBase.Files.Server.Services.TemporaryFileCleanupService` |

---

## DI Registration

```csharp
// Server Program.cs
// 1. Bind token signing key from secrets / environment
builder.Services.Configure<FileAccessTokenOptions>(
    builder.Configuration.GetSection("BlazorBaseFiles:TokenOptions"));

// 2. Register all server-side services (generic over the host DbContext, which is
//    bridged to the base DbContext type the controller and default authorizer need)
builder.Services.AddDbContext<AppDbContext>(/* ... */);
builder.Services.AddBlazorBaseFilesServer<AppDbContext>(options =>
{
    options.ControllerRoute              = "api/files";
    options.FileStorePath                = "/data/files";
    options.TempFileStorePath            = "/data/files/temp";
    options.MaxFileSizeBytes             = 50 * 1024 * 1024;
    options.AllowedContentTypes          = ["image/jpeg", "image/png", "image/webp"];
    options.UseImageThumbnails           = true;
    options.ImageThumbnailSize           = 400;
    options.ValidateContentTypeSignature = true;        // FILES-02: magic-byte check (default true)
    options.MaxImageDecodePixels         = 50_000_000;  // FILES-03: decode-bomb guard (default 50 MP)
    options.AutomaticallyDeleteOldTemporaryFiles = true;
    options.DeleteTemporaryFilesOlderThanSeconds = 86400; // 24 h
});

// 3. (Optional) Override the default access authorizer with a host-specific one
builder.Services.AddBlazorBaseFileAccessAuthorizer<MyHouseholdFileAccessAuthorizer>();
```

`AddBlazorBaseFilesServer<TContext>` (generic over the host's `DbContext`) registers:
- a scoped `DbContext` bridge (`DbContext` → the host's concrete `TContext`) so the controller and the default authorizer resolve the base `DbContext` without a separate host registration
- `IBlazorBaseFileOptions` (singleton)
- `IFileStorage` → `FileSystemFileStorage` (singleton)
- `IImageService` → `ImageSharpImageService` (singleton)
- `IFileAccessAuthorizer` → `OwnerScopedFileAccessAuthorizer` (**scoped** default, secure-by-default — grants access only when the file's `OwnerScopeKey` matches the caller; depends on the `DbContext`, see [Access Authorizer](#access-authorizer)); replace via `AddBlazorBaseFileAccessAuthorizer<T>()`, which also registers the host implementation **scoped** so it may depend on a `DbContext` or other per-request services
- `IFileAccessTokenService` → `HmacFileAccessTokenService` (singleton)
- `FileAccessTokenOptions` (options, configure via `Configure<FileAccessTokenOptions>` before calling this method)
- `TemporaryFileCleanupService` (hosted service)

---

## Token Signing Key

The HMAC-SHA256 signing key **must** be configured before the application starts. Store it as an
application secret or in an environment variable; never hardcode it.

```json
// appsettings.json (values from secrets manager / env in production)
{
  "BlazorBaseFiles": {
    "TokenOptions": {
      "SigningKeyBase64": "<base64-encoded 32+ random bytes>",
      "TokenLifetimeSeconds": 300
    }
  }
}
```

Generate a suitable key:

```bash
openssl rand -base64 32
```

The key is **validated at application startup** (`IValidateOptions<FileAccessTokenOptions>` +
`ValidateOnStart()`): a missing, non-base64, or shorter-than-32-byte key aborts startup with a clear
message (FILES-06) instead of failing lazily on the first token request. `TokenLifetimeSeconds`
defaults to **300** (5 minutes) — the token is the sole gate on `Download`/`Thumbnail`, so a short
lifetime bounds how long a leaked link stays usable.

---

## EF Core Integration

The host application owns the `DbSet<BaseFile>` and the migration. Call
`ApplyBaseFileConfiguration` from the host's `DbContext.OnModelCreating` override:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyBaseFileConfiguration();
    // ... other entity configurations ...
}
```

All column constraints (`[Key]`, `[Required]`, `[MaxLength]`, `[DatabaseGenerated]`) are expressed
as Data Annotations on `BaseFile` itself — `ApplyBaseFileConfiguration` only ensures the entity is
included in the model.

---

## Abstract Controller

Derive a thin concrete controller in the host application. ASP.NET Core discovers it via controller
scanning. The base already carries `[ApiController]`, `[Route("api/files")]`, and `[Authorize]`.

```csharp
// Trackventory.Server/Controllers/FileController.cs
[ApiController]
[Route("api/files")]
public class FileController(
    IFileStorage fileStorage,
    IImageService imageService,
    IFileAccessAuthorizer fileAccessAuthorizer,
    IFileAccessTokenService fileAccessTokenService,
    IBlazorBaseFileOptions options,
    TrackventoryDbContext dbContext)
    : BaseFileControllerBase(fileStorage, imageService, fileAccessAuthorizer, fileAccessTokenService, options, dbContext);
```

The base controller resolves `BaseFile` rows via `DbContext.Set<BaseFile>()`, so it never references
the concrete application context directly.

---

## REST Endpoints

| Method | Route | Auth | Description |
|---|---|---|---|
| `POST` | `/api/files` | Bearer | Upload a file (multipart/form-data). `OwnerScopeKey` defaults to the uploader when not supplied. Returns `FileUploadResult`. |
| `GET` | `/api/files/{id:guid}/token` | Bearer | Issue a short-lived signed access token for the file; gated by `CanAccessAsync` (owner-only under the default authorizer). |
| `GET` | `/api/files/{id:guid}?token=…` | Signed token | Download the full file. Range-request capable. Gated solely on the signed token — no per-request `CanAccessAsync` re-check. |
| `GET` | `/api/files/{id:guid}/thumbnail?token=…` | Signed token | Download the thumbnail (falls back to original). Gated solely on the signed token — no per-request `CanAccessAsync` re-check. |
| `DELETE` | `/api/files/{id:guid}` | Bearer | Delete the file row and its stored content; gated by `CanAccessAsync` (owner-only under the default authorizer). |

### Signed-Token Auth Scheme

Browser `<img>` and `<a>` tags cannot send a bearer token. The solution:

1. The client obtains a short-lived signed token via `GET /api/files/{id}/token` (bearer-authenticated). Issuing the token consults `IFileAccessAuthorizer.CanAccessAsync`, so only a caller entitled to the file ever receives one.
2. The token is appended as `?token=…` to the download/thumbnail URL.
3. The server validates the HMAC-SHA256 signature **and** expiry on every `Download`/`Thumbnail` request.
4. Possession of a valid token **is** the access check for `Download` and `Thumbnail` — there is no redundant per-request `CanAccessAsync` re-check on those two endpoints. This also lets a token the owner shares with an anonymous or third-party recipient (a "shared link") keep working for the token's lifetime, matching `[AllowAnonymous]` on both endpoints.

Unauthenticated or expired/invalid tokens receive `401 Unauthorized` from `Download`/`Thumbnail`.

**Per-file authorization at the trust boundary.** `GetToken` and `Delete` consult
`IFileAccessAuthorizer.CanAccessAsync` before acting, so a principal cannot obtain a token for — or
delete — a file it does not own by guessing its `Guid`. A failed check returns `404 Not Found`,
**identical to the response for a genuinely absent file** (FILES-06) — so an authenticated non-owner
cannot enumerate which file IDs exist by observing a `403`-vs-`404` difference. `Upload`
consults `CanAssignScopeAsync` so a caller cannot plant a file into a foreign `ownerScopeKey`. The
default authorizer (`OwnerScopedFileAccessAuthorizer`) restricts both checks to the caller's own
`NameIdentifier` claim (see [Access Authorizer](#access-authorizer)); a host needing a different posture
registers its own implementation, or the permissive opt-in `AllowAuthenticatedFileAccessAuthorizer`.

**Response hardening (stored-XSS defense).** Download and thumbnail responses set
`X-Content-Type-Options: nosniff` and `Content-Security-Policy: sandbox; default-src 'none'`. Downloads
are served with a `Content-Disposition: attachment` filename (never rendered inline). Thumbnails of
images are served as `image/jpeg`; a thumbnail request for a non-image falls back to the original bytes
served as `application/octet-stream` attachment — never inline with a client-controlled content type.

**Token posture (FILES-04, by design).** The download/thumbnail token is a **capability**, not a
per-user credential: it is deliberately not bound to a specific user identity so an owner can share a
working link with an anonymous or third-party recipient. It is carried as a `?token=…` query parameter
(the only channel a browser `<img>`/`<a>` can use), which means it can appear in proxy/access logs and
`Referer` headers; this residual exposure is bounded by the short `TokenLifetimeSeconds` (default 300 s).
A host that needs strict per-user, non-shareable access must register a custom `IFileAccessTokenService`
that binds a user claim into the token (and re-checks it on `Download`/`Thumbnail`).

### Upload Request

```
POST /api/files
Content-Type: multipart/form-data

file        (IFormFile)         — the file bytes
ownerScopeKey (string, optional) — opaque tenant/scope key stored in BaseFile.OwnerScopeKey
```

When `ownerScopeKey` is omitted, `BaseFile.OwnerScopeKey` defaults to the caller's `NameIdentifier`
claim, so an uploaded file is owned by whoever uploaded it unless an explicit scope is supplied.

Server-side validation:
- `IFileAccessAuthorizer.CanAssignScopeAsync(ownerScopeKey, …)` must return `true`, or the request is rejected with `403 Forbidden`.
- File length > 0.
- `file.Length ≤ MaxFileSizeBytes` (returns 400 if exceeded).
- `file.ContentType` matches `AllowedContentTypes` (empty list = all; wildcard `image/*` supported).
- **Length guards (FILES-05):** `ownerScopeKey` ≤ 200 chars and the derived file extension ≤ 20 chars, checked **before any storage write** so an over-length value returns `400` with no committed or orphaned file/DB row (previously a `500` plus an orphan).
- **Content-type signature check (FILES-02):** for sniffable types (JPEG/PNG/GIF/WEBP/BMP/PDF) the leading "magic bytes" must match the declared `ContentType`, or the upload is rejected with `400` — so a payload (e.g. HTML/script) mislabeled as an image cannot be stored. Text-based types without a reliable signature (`text/*`, `application/json`, `image/svg+xml`) are not sniffable and pass through. Toggle with `IBlazorBaseFileOptions.ValidateContentTypeSignature` (default `true`).
- **Image decompression-bomb guard (FILES-03):** before a thumbnail is decoded, the image header is identified and rejected with `400` when its pixel count exceeds `IBlazorBaseFileOptions.MaxImageDecodePixels` (default `50_000_000`) — a small crafted file with enormous dimensions can no longer force an unbounded-memory decode.

On success, returns `FileUploadResult`:
```json
{
  "id": "…",
  "fileName": "photo",
  "extension": ".jpg",
  "contentType": "image/jpeg",
  "fileSize": 102400,
  "hash": "…sha256hex…"
}
```

---

## Storage

`FileSystemFileStorage` stores files under two directories configured via `IBlazorBaseFileOptions`:

- **`FileStorePath`** — permanent store for committed files.
- **`TempFileStorePath`** — temporary directory for in-flight uploads, swept by `TemporaryFileCleanupService`.

On-disk naming is **never** derived from client-supplied filenames. The filename pattern is:

```
{fileId:N}{extension}          →  permanent file
{fileId:N}_thumb{extension}    →  thumbnail side-file
```

This prevents path-traversal attacks. The `extension` is taken from `Path.GetExtension(file.FileName).ToLowerInvariant()` and then stored in the database; it is validated indirectly via the content-type filter.

---

## Image Processing

`ImageSharpImageService` (SixLabors.ImageSharp 3.x, managed, Linux-safe) implements `IImageService`:

- `CreateThumbnailAsync` — resizes to fit `ImageThumbnailSize` max-edge, aspect preserved, saves as JPEG.
- `ResizeToMaxSizeAsync` — in-memory resize, returns JPEG bytes.

Non-image files are never decoded. The caller must guard with `IBaseFile.IsImage()` before invoking.

Supported formats: JPEG, PNG, WebP, GIF, BMP (ImageSharp 3.x built-in). HEIC (iPhone) is not
supported in the managed stack; convert client-side or handle at a higher layer.

---

## Access Authorizer

```csharp
public interface IFileAccessAuthorizer
{
    Task<bool> CanAccessAsync(Guid fileId, ClaimsPrincipal user, CancellationToken cancellationToken);
    Task<bool> CanAssignScopeAsync(string? ownerScopeKey, ClaimsPrincipal user, CancellationToken cancellationToken);
}
```

`CanAccessAsync` gates `GetToken` (token issuance) and `Delete` on an existing file; `CanAssignScopeAsync`
gates the `ownerScopeKey` a fresh `Upload` may claim. Neither `Download` nor `Thumbnail` calls
`CanAccessAsync` directly — access to those two endpoints is decided entirely by possession of a valid
signed token, which is only ever handed out by `GetToken` after `CanAccessAsync` succeeds (see
[Signed-Token Auth Scheme](#signed-token-auth-scheme)).

### Default: `OwnerScopedFileAccessAuthorizer` (secure-by-default, scoped)

The default implementation is `OwnerScopedFileAccessAuthorizer`, registered with a **scoped** lifetime
because it depends on the host's `DbContext`:

- `CanAccessAsync` loads the file and returns `true` only when `BaseFile.OwnerScopeKey` equals the
  caller's `ClaimTypes.NameIdentifier` claim — per-user ownership, nothing else.
- `CanAssignScopeAsync` returns `true` only when `ownerScopeKey` is `null` or equals the caller's own
  `NameIdentifier` — a caller may upload an unscoped file or claim its own scope, but can never plant a
  file under another user's scope.

Combined with `Upload` defaulting `OwnerScopeKey` to the uploader (see
[Upload Request](#upload-request)), every file is private to the user who owns it unless the host
registers a different authorizer.

### Opt-in: `AllowAuthenticatedFileAccessAuthorizer` (permissive, no longer default)

`AllowAuthenticatedFileAccessAuthorizer` permits **any** authenticated principal to access or claim any
file — the pre-fix, single-tenant/trusted-deployment posture. It is intentionally **not** registered by
default; a host that wants this behavior must opt in explicitly:

```csharp
services.AddBlazorBaseFileAccessAuthorizer<AllowAuthenticatedFileAccessAuthorizer>();
```

### Custom authorizers

Override **both** methods to enforce a different isolation rule — for example scoping by household
rather than by individual user:

```csharp
public class HouseholdFileAccessAuthorizer(IHouseholdAccessService access) : IFileAccessAuthorizer
{
    #region Injects
    private readonly IHouseholdAccessService Access = access;
    #endregion

    public async Task<bool> CanAccessAsync(Guid fileId, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var file = await Access.FindFileAsync(fileId, cancellationToken).ConfigureAwait(false);
        if (file is null)
            return false;
        var householdId = user.FindFirstValue("household_id");
        return file.OwnerScopeKey == householdId;
    }

    public Task<bool> CanAssignScopeAsync(string? ownerScopeKey, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var householdId = user.FindFirstValue("household_id");
        return Task.FromResult(ownerScopeKey is null || ownerScopeKey == householdId);
    }
}
```

Register with:

```csharp
services.AddBlazorBaseFileAccessAuthorizer<HouseholdFileAccessAuthorizer>();
```

Custom and opt-in authorizers are registered **scoped** by `AddBlazorBaseFileAccessAuthorizer<T>()` so
they may depend on a `DbContext` or other per-request services; the consuming `BaseFileControllerBase`
is itself scoped, so this is safe.

### Upgrade / breaking behavior

This is a **stricter default**. After this change, files whose `OwnerScopeKey` does not match an
authenticated user's `NameIdentifier` — including legacy files stored with a `null` or otherwise
non-matching scope under the old any-authenticated posture — become inaccessible under the new default
`OwnerScopedFileAccessAuthorizer` (`404 Not Found` from `GetToken`/`Delete` — uniform with an absent file,
see FILES-06 below; no token is ever issued for `Download`/`Thumbnail`). Hosts relying on the old "any
authenticated user can access any file" behavior must either:
- register `AllowAuthenticatedFileAccessAuthorizer` explicitly
  (`AddBlazorBaseFileAccessAuthorizer<AllowAuthenticatedFileAccessAuthorizer>()`), or
- backfill `OwnerScopeKey` on existing rows to the owning user's `NameIdentifier` (or another value a
  custom authorizer recognizes) before relying on the secure-by-default authorizer.

Admins/hosts that need genuinely cross-user or public file access must register a custom
`IFileAccessAuthorizer` (or the permissive opt-in above); access tokens themselves remain short-lived
HMAC tokens regardless of which authorizer is registered.

#### Medium/low hardening (work item #112)

Behavior/config changes bundled in this follow-up (no EF migration required):

- **FILES-06 — fail-fast signing key.** A missing, non-base64, or shorter-than-32-byte
  `FileAccessTokenOptions:SigningKeyBase64` now aborts application startup (`ValidateOnStart`) instead of
  failing lazily on the first token request. Hosts that registered Files without a valid key must supply one.
- **FILES-06 — uniform 404.** An unauthorized `GetToken`/`Delete` now returns `404 Not Found` (was `403`),
  identical to an absent file, so a non-owner cannot enumerate file IDs. Clients branching on `403` must adjust.
- **FILES-02 — magic-byte check (default on).** Uploads whose leading bytes contradict the declared content
  type (for sniffable types) are now rejected `400`. A host that relied on lax typing can opt out via
  `IBlazorBaseFileOptions.ValidateContentTypeSignature = false`.
- **FILES-03 — image decode cap (default on).** Images whose pixel count exceeds
  `IBlazorBaseFileOptions.MaxImageDecodePixels` (default 50 MP) are rejected `400` before decoding, and
  only the first frame of an animated image is decoded (frame-bomb guard). Raise `MaxImageDecodePixels`
  (or set `≤ 0` for unlimited) if legitimate very-high-resolution uploads are rejected.
- **FILES-05 — upload length guards.** Over-length `ownerScopeKey`/extension/`FileName`/`ContentType` now
  return `400` before any storage write (previously a `500` plus an orphaned file).

---

## Temp-File Cleanup

`TemporaryFileCleanupService` runs as a `BackgroundService` that sweeps `TempFileStorePath` every
30 minutes when `AutomaticallyDeleteOldTemporaryFiles` is `true`. Files older than
`DeleteTemporaryFilesOlderThanSeconds` are deleted.

Safety guards:
- Refuses to operate when `TempFileStorePath` is null, empty, or resolves to a filesystem root (logs a warning and skips the sweep instead of throwing).
- Non-deletable files are logged as warnings and skipped; the sweep continues.

---

## Options

See [BlazorBase.Files.md](BlazorBase.Files.md) for the full `IBlazorBaseFileOptions` table.
Additional server-only options:

| Class | Property | Default | Description |
|---|---|---|---|
| `FileAccessTokenOptions` | `SigningKeyBase64` | `""` (must set) | Base64-encoded HMAC key, ≥ 32 bytes. Validated at startup (FILES-06). |
| `FileAccessTokenOptions` | `TokenLifetimeSeconds` | `300` | Download/thumbnail token TTL in seconds (the token is the sole gate — kept short). |

---

## Kestrel / Form Multipart Limits

The default Kestrel multipart body limit (30 MB) and form value limit may reject large uploads
before the controller is reached. Configure in the host:

```csharp
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024; // 100 MB
});

builder.WebHost.ConfigureKestrel(server =>
{
    server.Limits.MaxRequestBodySize = 100 * 1024 * 1024;
});
```

---

## Project Dependencies

```
BlazorBase.Files.Server
  └─ BlazorBase.Files          (browser-safe RCL — models, options, IFileUploadClient)
  └─ BlazorBase.CRUD           (AuditModel)
  └─ Microsoft.AspNetCore.App  (framework reference)
  └─ SixLabors.ImageSharp 3.x  (managed image processing, Apache 2.0 / SixLabors license)
```
