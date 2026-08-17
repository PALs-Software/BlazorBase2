# BlazorBase.DataProtection

A thin wrapper around ASP.NET Core Data Protection that gives BlazorBase a single, static
`Protect`/`Unprotect` (and `string` extension) surface for encrypting values outside of dependency
injection, while still letting the host fully control key management for containers and server farms.

> Target framework: `net10.0` · Depends on `Microsoft.AspNetCore.DataProtection` only (no web hosting
> types), so it can be referenced transitively by browser-safe libraries such as `BlazorBase.CRUD`.

---

## Overview

| Concern | Type |
|---|---|
| DI registration + key management hook | `BlazorBaseDataProtectionServiceCollectionExtensions.AddBlazorBaseDataProtection` |
| Startup initialization | `BlazorBaseDataProtectionApplicationBuilderExtensions.UseBlazorBaseDataProtection` |
| Static protect/unprotect surface | `BlazorBaseDataProtection.Protect` / `BlazorBaseDataProtection.Unprotect` / `BlazorBaseDataProtection.IsInitialized` |
| Purpose-scoped (context-bound) overloads | `BlazorBaseDataProtection.Protect(text, purpose)` / `Unprotect(cipher, purpose)` (DP-02) |
| `string` convenience extensions | `EncryptString` / `DecryptStringToInsecureString` (each with an optional purpose overload) |

---

## Registration

```csharp
// Program.cs

// 1. Register Data Protection, configuring key management for the deployment target.
builder.Services.AddBlazorBaseDataProtection(dataProtectionBuilder =>
{
    dataProtectionBuilder
        .PersistKeysToFileSystem(new DirectoryInfo("/keys"))
        .SetApplicationName("MyApp")
        .ProtectKeysWithCertificate(myKeyEncryptionCertificate);
});

var app = builder.Build();

// 2. Initialize the static helper once, after the service provider is built.
app.Services.UseBlazorBaseDataProtection();
```

`AddBlazorBaseDataProtection(configure)` calls `services.AddDataProtection()` and, if supplied,
invokes `configure` against the returned `IDataProtectionBuilder`. The `configure` callback is
optional (`Action<IDataProtectionBuilder>? configure = null`), so existing
`AddBlazorBaseDataProtection()` call sites keep compiling unchanged.

### Container guidance — persist keys to a mounted volume

Containers have an ephemeral, throwaway filesystem by default. Without explicit configuration, ASP.NET
Core falls back to an in-memory key ring: every restart generates a fresh key, and **all previously
protected data becomes permanently unreadable**. Persist keys to a directory backed by a mounted
volume so they survive restarts and redeploys:

```csharp
dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo("/keys"));
```

### Farm guidance — a stable application name and a shared key store

Across a farm of nodes, ASP.NET Core derives the key ring's isolation identity from the application
name, which defaults to the content-root path. If that path differs per node (or per deployment), each
node effectively uses a different key ring and data encrypted on one node cannot be decrypted on
another. Set an explicit, stable name shared by every node, and point every node at the **same**
persisted key store (a shared network path, blob storage, etc.):

```csharp
dataProtectionBuilder
    .SetApplicationName("MyApp")
    .PersistKeysToFileSystem(new DirectoryInfo("/mnt/shared/keys"));
```

### Keys at rest

Persisted keys are stored unencrypted by default. Encrypt them at rest with a certificate, DPAPI-NG, or
another `ProtectKeysWith*` call:

```csharp
dataProtectionBuilder.ProtectKeysWithCertificate(myKeyEncryptionCertificate);
```

Omitting `configure` entirely uses ASP.NET Core's default key management — acceptable for local
development and single-instance hosts with a durable filesystem, but **not safe** for containers
(ephemeral keys) or multi-node farms (unshared keys per node).

---

## Usage

Call `UseBlazorBaseDataProtection()` exactly once at startup, after the service provider has been
built (`app.Services.UseBlazorBaseDataProtection()`). This resolves `IDataProtectionProvider` and
initializes the static `BlazorBaseDataProtection` helper so that `Protect`/`Unprotect` and the
`EncryptString`/`DecryptStringToInsecureString` extension methods can be used from contexts without
dependency injection (e.g. static helpers, EF Core value converters).

```csharp
var ciphertext = BlazorBaseDataProtection.Protect(plainText);
var roundTripped = BlazorBaseDataProtection.Unprotect(ciphertext);

var ciphertextFromExtension = "my-secret-token".EncryptString();
var plainTextFromExtension = ciphertextFromExtension.DecryptStringToInsecureString();
```

`Protect`/`Unprotect` throw `InvalidOperationException` if called before
`app.Services.UseBlazorBaseDataProtection()` has run. `UseBlazorBaseDataProtection` takes
`IServiceProvider` rather than `IApplicationBuilder` so this library has no dependency on ASP.NET
Core's web hosting types, which are unavailable to Blazor WebAssembly client projects that reference it
transitively via `BlazorBase.CRUD`.

### Purpose-scoped (context-bound) encryption (DP-02)

The no-purpose `Protect`/`Unprotect` above all derive from one shared key, so a ciphertext produced for
one field can be pasted into another and still decrypts — there is no context binding. To bind a
ciphertext to a *context* (an entity, a field, a tenant), use the purpose-scoped overloads:

```csharp
var emailCipher = BlazorBaseDataProtection.Protect(user.Email, "User.Email");
var email       = BlazorBaseDataProtection.Unprotect(emailCipher, "User.Email");

var tokenCipher = apiToken.EncryptString("Integration.ApiToken");
var apiToken2   = tokenCipher.DecryptStringToInsecureString("Integration.ApiToken");
```

A ciphertext encrypted under one `purpose` **cannot** be decrypted under a different purpose (or under
the no-purpose path) — `Unprotect` throws `CryptographicException`, and the `DecryptStringToInsecureString`
extension returns `null`. So moving `User.Email`'s ciphertext into `User.SocialSecurityNumber` no longer
silently decrypts. Choose a stable, descriptive purpose per encrypted field (e.g. `"<Entity>.<Property>"`)
and never change it for existing data — the purpose is part of the key derivation, so changing it makes
already-stored ciphertext unreadable. The purpose **must** come from a bounded, developer-controlled set
of constants — never derive it from untrusted or per-record data (e.g. `$"User.Email.{userId}"`): each
distinct purpose creates a cached protector, so a high-cardinality purpose grows that cache without bound
(a memory-exhaustion vector). `UseBlazorBaseDataProtection()`/`Initialize` is a **startup-once** call and
is not designed to be re-invoked while the application is serving requests.

**Back-compat:** the purpose-scoped methods are strictly additive. The no-purpose `Protect`/`Unprotect`
(and the parameterless `EncryptString`/`DecryptStringToInsecureString`) are byte-for-byte unchanged, so
all data encrypted before this change keeps decrypting. Adopt purposes for *new* encrypted fields; there
is no need (and no safe way) to re-key existing no-purpose ciphertext in place without a read-old /
write-new migration.

### Note on plaintext lifetime (DP-03)

The API operates on `string`, which is immutable and cannot be zeroed — a decrypted secret lingers in
managed memory until garbage-collected (the method name `DecryptStringToInsecureString` flags this
deliberately). For a host that must minimize a secret's in-memory lifetime, a `byte[]`/`ReadOnlySpan<byte>`
overload (paired with `CryptographicOperations.ZeroMemory`) can be added additively without breaking the
string API; that is a documented follow-up, not part of this change.

---

## Operational Note

Omitting key persistence configuration means the key ring is ephemeral (in-memory) in a container —
ASP.NET Core itself logs its own warning when it falls back to the in-memory `EphemeralXmlRepository`,
so watch application logs at startup for that message. A farm where each node persists keys **without**
sharing them (or without a stable, common `SetApplicationName`) does not fail loudly: each node happily
encrypts and decrypts its own data, and only cross-node decryption fails, typically surfacing later as
sporadic decryption errors on specific nodes. Operators must verify — as part of deployment, not at
runtime — that every node in a farm points at the same persisted key store and uses the same
application name.

---

## Project Dependencies

```
BlazorBase.DataProtection
  └─ Microsoft.AspNetCore.DataProtection  (package reference)
```
