# BlazorBase.DataProtection.AzureKeyVault

Opt-in key management for ASP.NET Core Data Protection: the key ring is stored in the application's
own database and encrypted at rest with an Azure Key Vault key.

Nothing applies this automatically. `AddBlazorBaseDataProtection` keeps whatever key management the
host configures — which, if the host configures none, is ASP.NET Core's default: keys written to the
local machine or user profile, unencrypted, and bound to that machine.

---

## Why this matters more than it used to

In BlazorBase2 the Data Protection key ring **is** the authentication key material.

`BlazorBase.User.Server` no longer signs its own JWT with a secret from configuration. It never calls
`AddDataProtection()` at all — it only consumes `IDataProtectionProvider`. Everything the
authentication stack hands out is protected by whatever key ring the host configured:

| Artefact | Where it comes from |
|---|---|
| Identity bearer access and refresh tokens | `Services/AuthTokenIssuer.cs`, through the bearer scheme's protectors |
| Identity application cookie | the cookie handler |
| Antiforgery token | ASP.NET Core antiforgery |
| Two-factor pending ticket | `Services/TwoFactorPendingTokenService.cs` |

Two consequences follow, and they are the reason this library exists:

- **Lose the key ring and every user is signed out.** With the default key location that happens on
  every restart of a container, every redeploy, and every scale event that lands on a fresh instance.
- **Fail to share the key ring and instances cannot read each other's sessions.** This does not fail
  cleanly. It shows up as sporadic 401s that follow the load balancer rather than the user, and it is
  miserable to diagnose from the symptom.

Under BlazorBase v1, where a configured secret signed the tokens, the key ring mattered far less to
authentication. That is no longer true, so key management is now an authentication concern rather
than only a "some encrypted fields became unreadable" concern.

---

## Key Vault is the lock, the database is the box

These are two separate jobs and it is easy to believe one of them covers both.

`ProtectKeysWithAzureKeyVault` **wraps** the key ring. It encrypts the keys at rest; it stores
nothing. On its own it leaves the keys exactly where they were — in the local profile, ephemeral.

`PersistKeysToDbContext` **stores** the key ring. It puts the keys in your database, so they survive
a restart, travel with your database backup, and are visible to every instance.

This library does both, in that order, plus a stable application name. Using only the first is a
common and expensive mistake.

---

## Registration

```csharp
using BlazorBase.DataProtection.AzureKeyVault;

builder.Services.AddBlazorBaseAzureKeyVaultDataProtection<ApplicationDbContext>(settings =>
{
    settings.ApplicationName            = "MyApp";
    settings.AzureKeyVaultKeyIdentifier = new Uri(builder.Configuration["DataProtection:KeyVaultKeyUri"]!);
});
```

Call this **instead of** `AddBlazorBaseDataProtection`, not alongside it. Internally it goes through
the same entry point, so the rest of the framework — including
`BlazorBaseDataProtectionApplicationBuilderExtensions` — behaves exactly as before.

`ApplicationName` and `AzureKeyVaultKeyIdentifier` are required and throw at registration when
missing. `TokenCredential` defaults to `DefaultAzureCredential`: managed identity on Azure, developer
sign-in locally. `Configure` is a raw `IDataProtectionBuilder` hook applied last, for anything this
settings class does not cover.

### What the host's DbContext must provide

```csharp
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
}
```

Then add and apply an EF Core migration for the `DataProtectionKeys` table **before** the first
deployment that uses this. Without the table the application starts and fails on the first request
that needs a key.

---

## Three settings that are easy to get subtly wrong

**Do not put a key version in the identifier.** Use
`https://my-vault.vault.azure.net/keys/dataprotection-key`, not `.../dataprotection-key/abc123…`.
Pinning a version means a key rotation in Key Vault silently stops being picked up, and you discover
it when the old version is eventually disabled.

**`ApplicationName` must be stable and identical on every instance.** It is what isolates this
application's keys from anything else sharing the store — and what makes several instances agree they
are the same application. Left unset, ASP.NET Core derives it from the content root path, so two
instances with different deployment paths silently do not share keys even though they share a
database.

**The identity needs the right role on the vault**, not on the key ring: permission to wrap and
unwrap with that key. See [the setup guide](../BlazorBase.DataProtection.AzureKeyVault/Documentation/AzureKeyVaultSetup.md)
for the exact roles and a provisioning script.

---

## Operational consequences

**Cold start needs Key Vault.** The unwrapped key ring is cached in the process for roughly 24 hours,
so a brief Key Vault outage is usually invisible to a running application. But the first request after
a restart, a deployment or a scale-out has to unwrap the key ring, and that call must succeed. Treat
Key Vault as a start-up dependency: health checks, retries, and monitoring.

**Backups only look complete.** The `DataProtectionKeys` table is inside your application database, so
your normal backup covers it. The Key Vault key is not in that backup. Restoring a database into an
environment that cannot reach that key gives you rows nobody can decrypt.

**The blast radius of losing the key is total.** If the Key Vault key is deleted and its soft-delete
retention lapses, the persisted key ring cannot be unwrapped again. Every session ends, and every
value ever encrypted through the field-level protector in `BlazorBase.DataProtection` becomes
permanently unreadable. That is data loss, not an inconvenience — purge protection on the vault is
worth the small operational friction.

**Moving to a new machine is cheap, by design.** The key ring lives in the database and the identity
that unwraps it is not tied to a VM disk, so a move needs no re-encryption — only the managed identity
has to be reassigned. See
[the VM move runbook](../BlazorBase.DataProtection.AzureKeyVault/Documentation/VmMoveRunbook.md).

---

## When you do not need this

This library is not automatically the right answer.

A single instance with a durable, backed-up key location — a mounted volume that survives redeploys —
already solves the "everyone is signed out on restart" problem, and
`AddBlazorBaseDataProtection(builder => builder.PersistKeysToFileSystem(…).SetApplicationName(…))`
is enough. Key Vault adds encryption of the keys at rest and a start-up dependency along with it.

Take it when the keys must not sit readable in a volume or a database, when several instances share
one store, or when your compliance position asks where key material is encrypted. Do not take it
merely because it sounds more secure than a file path.

---

## See also

- [BlazorBase.DataProtection.md](BlazorBase.DataProtection.md) — the base library, the field-level
  protector, and container and farm guidance
- [BlazorBase.User.Server.AuthTransports.md](BlazorBase.User.Server.AuthTransports.md) — what the key
  ring is protecting on the authentication side
- [Setup guide](../BlazorBase.DataProtection.AzureKeyVault/Documentation/AzureKeyVaultSetup.md) and
  [provisioning script](../BlazorBase.DataProtection.AzureKeyVault/Scripts/Setup-AzureKeyVaultDataProtection.ps1)
