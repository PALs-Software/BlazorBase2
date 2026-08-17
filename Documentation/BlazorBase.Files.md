# BlazorBase.Files

Browser-safe Razor Class Library (net10.0, `SupportedPlatform browser`) providing generic, reusable
file/photo upload components, metadata models, and a client HTTP upload service. The server-side
storage, thumbnail generation, and controller live in the companion library **BlazorBase.Files.Server**
(see: [BlazorBase.Files.Server.md](BlazorBase.Files.Server.md)).

---

## Overview

| Concern | Type |
|---|---|
| File metadata entity | `BlazorBase.Files.Models.BaseFile` |
| Metadata contract | `BlazorBase.Files.Models.IBaseFile` |
| Options | `BlazorBase.Files.Models.IBlazorBaseFileOptions` / `BlazorBaseFileOptions` |
| Upload result | `BlazorBase.Files.Models.FileUploadResult` |
| Client upload service | `BlazorBase.Files.Services.IFileUploadClient` / `HttpFileUploadClient` |
| Single-photo card input | `BlazorBase.Files.Components.BaseFilePhotoInput.BaseFilePhotoInputComponent` |
| Multi-photo card input | `BlazorBase.Files.Components.BaseFileGalleryInput.BaseFileGalleryInputComponent` |
| Full-screen image viewer | `BlazorBase.Files.Components.BaseFileModal.BaseFileModalComponent` |
| List-cell thumbnail display | `BlazorBase.Files.Components.BaseFileCell.BaseFileCellComponent` |
| DI registration | `AddBlazorBaseFiles(IServiceCollection, Action<IBlazorBaseFileOptions>?)` |

---

## DI Registration

```csharp
// Client (WebAssembly) Program.cs
builder.Services.AddBlazorBaseFiles(options =>
{
    options.ControllerRoute = "api/files";
    options.MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB
    options.AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];
    options.UseImageThumbnails = true;
    options.ImageThumbnailSize = 400;
});

// The host must also register a typed HttpClient for IFileUploadClient with the
// correct base address and the auth token delegating handler:
builder.Services.AddHttpClient<IFileUploadClient, HttpFileUploadClient>(client =>
{
    client.BaseAddress = new Uri("https://myapp.example/api/files/");
}).AddHttpMessageHandler<AuthTokenHandler>();
```

`AddBlazorBaseFiles` does not register the `HttpClient`; the host configures it because the auth
handler, base address, and retry policy are host concerns.

---

## Models

### `IBaseFile`

```csharp
public interface IBaseFile
{
    Guid Id { get; set; }
    string FileName { get; set; }         // without extension
    string Extension { get; set; }        // with leading dot, e.g. ".jpg"
    string ContentType { get; set; }      // MIME type
    long FileSize { get; set; }
    string? Hash { get; set; }            // SHA-256 hex, for cache-busting
    string? OwnerScopeKey { get; set; }   // opaque tenant/scope key
    bool IsImage();

    // Base URL helpers (no token — stable for caching, link generation)
    string GetDownloadUrl(string route);
    string GetThumbnailUrl(string route);

    // Token-bearing URL helpers — append a short-lived signed token
    // obtained from IFileUploadClient.GetAccessTokenAsync before rendering
    // <img> or <a> tags that target protected file endpoints.
    // When token is null or empty these return the same URL as the no-token overloads.
    // The token is URL-encoded and appended with the correct ? or & separator.
    string GetDownloadUrl(string route, string? token);
    string GetThumbnailUrl(string route, string? token);
}
```

### `BaseFile : AuditModel, IBaseFile`

Pure metadata entity. No `[BaseCrud]`, no IO, no static options. The host application owns the
`DbSet<BaseFile>` and the migration; the server library provides `ApplyBaseFileConfiguration`.

```csharp
[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
public Guid Id { get; set; }

[Required, MaxLength(260)]  public string FileName { get; set; }
[Required, MaxLength(20)]   public string Extension { get; set; }
[Required, MaxLength(150)]  public string ContentType { get; set; }
public long FileSize { get; set; }
[MaxLength(64)]  public string? Hash { get; set; }
[MaxLength(200)] public string? OwnerScopeKey { get; set; }
```

### `FileUploadResult`

```csharp
public sealed record FileUploadResult(
    Guid Id, string FileName, string Extension,
    string ContentType, long FileSize, string? Hash);
```

Returned by the server after a successful upload and surfaced to the client component.

---

## Options (`IBlazorBaseFileOptions`)

| Property | Default | Description |
|---|---|---|
| `ControllerRoute` | `"api/files"` | Base path of the server's file controller. |
| `FileStorePath` | `"FileStore"` | Server-side permanent store directory. |
| `TempFileStorePath` | `"FileStore/Temp"` | Server-side temporary upload directory. |
| `MaxFileSizeBytes` | `52428800` (50 MB) | Global max upload size, overridable per property via `[MaxFileSize]`. |
| `AllowedContentTypes` | `[]` (all) | Allowed MIME types; empty means all are accepted. |
| `UseImageThumbnails` | `true` | Whether the server generates thumbnails for image uploads. |
| `ImageThumbnailSize` | `400` | Max-edge pixel size of generated thumbnails. |
| `ValidateContentTypeSignature` | `true` | Server-side magic-byte check (FILES-02): rejects an upload whose leading bytes contradict its declared content type, for sniffable types (JPEG/PNG/GIF/WEBP/BMP/PDF). Set `false` to disable. |
| `MaxImageDecodePixels` | `50000000` (50 MP) | Server-side image decode-bomb guard (FILES-03): an image whose pixel count exceeds this is rejected `400` before decoding. `≤ 0` = unlimited. Raise it if legitimate very-high-resolution uploads are rejected. |
| `AutomaticallyDeleteOldTemporaryFiles` | `true` | Enables the server-side cleanup hosted service. |
| `DeleteTemporaryFilesOlderThanSeconds` | `86400` (24 h) | Age threshold for orphaned temp files. |

> **Content-type policy.** The default empty `AllowedContentTypes` accepts every type. Magic-byte
> validation only covers binary raster/PDF signatures, so text-based active-content types
> (`text/html`, `image/svg+xml`, `application/xhtml+xml`) are not sniffable and pass the upload check —
> stored-XSS is prevented at serve time (downloads are `Content-Disposition: attachment` + `nosniff` +
> CSP `sandbox`, thumbnails are re-encoded to `image/jpeg`). If you set an `AllowedContentTypes`
> allowlist, **exclude `image/svg+xml`** (or keep serving it as an attachment) so an SVG cannot be
> rendered inline as active content. Prefer a closed allowlist over the accept-all default.

---

## Attributes

| Attribute | Target | Effect |
|---|---|---|
| `[MaxFileSize(MaxFileSizeBytes = n)]` | Property | Overrides the global `MaxFileSizeBytes` for this property. |
| `[FileInputFilter(Filter = "image/*")]` | Property | Sets the `accept` attribute on the file input. |
| `[HideFilePreview]` | Property | Suppresses the inline preview in the upload component. |
| `[AllowCameraCapture]` | Property | On mobile: adds `accept="image/*"` and `capture="environment"`. |

---

## Client Upload Service

```csharp
public interface IFileUploadClient
{
    Task<FileUploadResult> UploadAsync(
        Stream content, string fileName, string contentType,
        string? ownerScopeKey, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    // Fetches a short-lived signed access token from GET {id}/token (bearer-authenticated).
    // Returns the token string, or an empty string when the server returns null.
    // Components call this immediately after UploadAsync, or in OnParametersSetAsync for
    // existing files, and pass the result to GetDownloadUrl/GetThumbnailUrl token overloads.
    Task<string> GetAccessTokenAsync(Guid id, CancellationToken cancellationToken);
}
```

`HttpFileUploadClient` sends a `multipart/form-data` POST with a `file` part and an optional
`ownerScopeKey` string part. The `HttpClient` base address must point to the controller route
(e.g. `https://host/api/files/`). Authentication is the host's responsibility via a delegating
handler — this same handler attaches the bearer token to the `GET {id}/token` call, so the token
endpoint is also reachable without additional host configuration beyond the base-address setup.

---

## Components

### `BaseFilePhotoInputComponent`

Implements `IBaseCustomPropertyInput` + `ICardSaveParticipant`. Handles a `Guid` property decorated
with any file attribute. The CRUD card's `CanHandle` check matches on attribute presence; the first
registered input whose `CanHandle` returns `true` renders. Parity: mirrors `UserPasswordInput`'s
registration and `CanHandle` shape.

**Behavior:**
- File is uploaded immediately on selection (`FluentInputFile.OnFileUploaded`).
- The returned `Guid` ID is pushed to the model via `ValueChanged`.
- Immediately after upload, `IFileUploadClient.GetAccessTokenAsync` is called for the new file's ID
  and the token is stored in component state. The thumbnail URL uses this token.
- When `OnParametersSetAsync` fires with a new `CurrentFileId` (e.g. when the card loads an existing
  record), the component fetches an access token for that file; the token is cached and only re-fetched
  when the ID changes.
- When `[AllowCameraCapture]` is present: `accept="image/*"` + `capture="environment"`.
- When `[FileInputFilter]` is present: its `Filter` value is the `accept` attribute.
- Shows a thumbnail preview (suppressed by `[HideFilePreview]`).
- `ValidateAsync`: blocks save if an upload error is pending.
- `OnAfterSaveAsync`: clears the pending upload result and the cached access token.

### `BaseFileGalleryInputComponent`

Multi-photo variant. Handles `List<Guid>` / `IList<Guid>` properties with file attributes.
Each uploaded file appends to the list; removing a file deletes it via `IFileUploadClient.DeleteAsync`
and removes its ID from the list.

After each successful upload, `IFileUploadClient.GetAccessTokenAsync` is called for that file's ID and
the token is cached in a `Dictionary<Guid, string>`. Thumbnail URLs are built with the cached token via
`GetThumbnailUrl(route, token)`. The token cache is cleared when a file is removed or after save.

### `BaseFileModalComponent`

Full-screen image viewer in a `FluentDialog`. Not a `IBaseCustomPropertyInput`; rendered by a host
page or component via a `@ref`. Requires `IFileUploadClient` to be injectable (registered by the host
alongside the `HttpClient` for the file controller).

```csharp
// In markup
<BaseFileModalComponent @ref="Modal" />

// In code-behind — use await so the token is fetched before the dialog renders
private BaseFileModalComponent? Modal;
await Modal.OpenAsync(fileUploadResult);
```

`OpenAsync` fetches a signed access token from `GET {id}/token` before setting `IsVisible = true`,
ensuring the download `<img>` and `<a>` URLs are token-bearing on first render.

For files already on the server, construct a `FileUploadResult` from stored `BaseFile` metadata.

### `BaseFileCellComponent`

Implements `IBaseCustomPropertyDisplay`. Renders a 40 × 40 px thumbnail in a CRUD list cell.
URL shape: `/{route}/{id}/thumbnail` — the hash is not appended here because the cell has no access
to the stored hash at list-render time (the list only receives the raw ID value). The server must
set appropriate cache headers on the thumbnail endpoint; the host can supply a hash via a token query
param when the full `BaseFile` object is available.

On image load failure: the `<img>` is hidden via an `onerror` attribute and a placeholder icon is
shown. For non-image content types: a document icon placeholder is rendered.

---

## URL and Auth Convention

`GetDownloadUrl(route)` returns `/{route}/{id}[?h={hash}]`.
`GetThumbnailUrl(route)` returns `/{route}/{id}/thumbnail[?h={hash}]`.

`GetDownloadUrl(route, token)` and `GetThumbnailUrl(route, token)` append `token=<encoded>` using
`&` when a `?h=` hash param is already present, `?` otherwise. Passing `null` or empty returns the
same URL as the no-token overload.

`<img>` and `<a>` tags cannot send a bearer token. The server companion (`BlazorBase.Files.Server`)
accepts a **short-lived signed token** as a query parameter (e.g. `?token=…`) for anonymous-looking
requests to file endpoints. The built-in display components (`BaseFilePhotoInputComponent`,
`BaseFileGalleryInputComponent`, `BaseFileModalComponent`) all call
`IFileUploadClient.GetAccessTokenAsync` in async lifecycle hooks (or immediately after upload) and
build URLs via the token-aware overloads. The host's `HttpClient` for `IFileUploadClient` must have
its bearer-auth handler attached so that the `GET {id}/token` call is authenticated.

Custom callers that render file URLs outside the provided components should follow the same pattern:
call `GetAccessTokenAsync`, then pass the token to `GetDownloadUrl`/`GetThumbnailUrl` before binding
to `<img src>` or `<a href>`.

---

## Data Flow

```
Pick/capture file
       │
       ▼
Client-side guards (size from [MaxFileSize] or options; type from [FileInputFilter] or options)
       │
       ▼
IFileUploadClient.UploadAsync  ──POST multipart──►  Server (BlazorBase.Files.Server)
                                                         │ streams → IFileStorage
                                                         │ thumbnails via IImageService
                                                         │ persists BaseFile row
                                                         │ returns FileUploadResult
       ◄──────────────────────────────────────────────────
       │
       ▼
IFileUploadClient.GetAccessTokenAsync  ──GET {id}/token──►  Server
                                                                │ issues short-lived signed token
       ◄────────────────────────────────────────────────────────
       │  token cached in component state
       ▼
Component stores Guid → pushes via ValueChanged → model property set
Thumbnail/download URLs built with token via GetThumbnailUrl(route, token)
       │
       ▼ (on card save)
ICardSaveParticipant.OnBeforeSaveAsync  (validation only — no DB access)
CRUD card persists model (which references the Guid)
ICardSaveParticipant.OnAfterSaveAsync   (clears pending result)
```

Orphaned uploads (cancelled card, navigated away) are reclaimed by the server-side
`TemporaryFileCleanupService`.

---

## Solution Membership

`BlazorBase.Files` is registered in `BlazorBase.slnx`. Its server companion
`BlazorBase.Files.Server` is also registered in `BlazorBase.slnx`.

---

## Project Dependencies

```
BlazorBase.Files
  └─ BlazorBase.CRUD
  └─ Microsoft.FluentUI.AspNetCore.Components 4.14.0
  └─ Microsoft.FluentUI.AspNetCore.Components.Icons 4.14.0
  └─ Microsoft.Extensions.Http
  └─ Microsoft.Extensions.Localization
```

No server-only, native, or image-processing packages (`Microsoft.AspNetCore.App`,
`SixLabors.ImageSharp`, `Magick.NET`, `System.Drawing.Common`) are referenced here.
