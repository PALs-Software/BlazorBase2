# BlazorBase.DataProtection.AzureKeyVault

Opt-in Azure Key Vault backed configuration for ASP.NET Core Data Protection in BlazorBase applications.

This package is **not** applied automatically by `BlazorBase.DataProtection` - the default
`AddBlazorBaseDataProtection(...)` call keeps using whatever key management the host configures (or ASP.NET
Core's own default, machine/user-bound behaviour, if the host configures none). Reference this package and
call `AddBlazorBaseAzureKeyVaultDataProtection<TDbContext>(...)` explicitly when you want:

- the Data Protection key ring persisted in your application's database (`PersistKeysToDbContext`), so it travels
  with your database backups and is independent of the machine the app runs on, and
- the key ring protected at rest by an Azure Key Vault RSA key (`ProtectKeysWithAzureKeyVault`), so the raw key
  material is never stored unencrypted.

## Why both a DB store *and* Key Vault?

`ProtectKeysWithAzureKeyVault` only **wraps** (encrypts) the Data Protection key ring with a Key Vault key. It does
**not** provide storage for the key ring itself - you still need `PersistKeysTo...` (DB, Blob Storage, file system).
Key Vault is the lock, the database is the box. Without an explicit persistence store, Data Protection falls back to
the local machine/user profile (`%LOCALAPPDATA%\ASP.NET\DataProtection-Keys` on Windows), which is exactly the
machine/user-bound behaviour this package is meant to replace.

## Why this matters more in BlazorBase2 than it did before

`BlazorBase.User.Server` no longer issues a self-signed JWT with a configured secret - it authenticates entirely
through ASP.NET Core Identity's own bearer tokens, application cookie, antiforgery token and two-factor pending
ticket, all of which are protected through the Data Protection key ring. Losing or not sharing that key ring is
therefore an authentication outage, not just a "some encrypted values became unreadable" problem. See
[../Documentation/BlazorBase.DataProtection.AzureKeyVault.md](../Documentation/BlazorBase.DataProtection.AzureKeyVault.md)
for the full explanation.

## Usage

```csharp
using Azure.Identity;
using BlazorBase.DataProtection.AzureKeyVault;

// instead of AddBlazorBaseDataProtection(...)
builder.Services.AddBlazorBaseAzureKeyVaultDataProtection<ApplicationDbContext>(settings =>
{
    settings.ApplicationName = "MyApp";
    settings.AzureKeyVaultKeyIdentifier = new Uri(builder.Configuration["DataProtection:KeyVaultKeyUri"]!);
    // settings.TokenCredential defaults to DefaultAzureCredential() (Managed Identity in Azure, developer sign-in locally)
});
```

`ApplicationDbContext` must implement `IDataProtectionKeyContext` (from `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`):

```csharp
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
}
```

Add and apply an EF Core migration for the new `DataProtectionKeys` table before first deployment.

## Required Azure resources

See [Documentation/AzureKeyVaultSetup.md](Documentation/AzureKeyVaultSetup.md) for the full setup guide (resources, RBAC roles,
Managed Identity, appsettings, availability/caching behaviour, backup considerations) and
[Scripts/Setup-AzureKeyVaultDataProtection.ps1](Scripts/Setup-AzureKeyVaultDataProtection.ps1) for a ready-to-run
provisioning script.

## Moving to a new VM

See [Documentation/VmMoveRunbook.md](Documentation/VmMoveRunbook.md) and
[Scripts/Move-AzureKeyVaultDataProtectionIdentity.ps1](Scripts/Move-AzureKeyVaultDataProtectionIdentity.ps1).
Because the key ring lives in the database (not on the VM) and the Managed Identity used to unwrap it is
independent of the VM's disk, moving to a new VM requires no re-encryption of application data - only the identity
needs to be reassigned.
