# Azure Key Vault Data Protection setup

This document describes the Azure resources, roles and application configuration required to use
`AddBlazorBaseAzureKeyVaultDataProtection<TDbContext>(...)`.

## What gets deployed

| Resource | Purpose |
| --- | --- |
| Azure Key Vault (or Managed HSM) | Holds the RSA key that wraps (encrypts) the Data Protection key ring at rest. |
| RSA key inside the vault | The actual wrapping key used by `ProtectKeysWithAzureKeyVault`. |
| Managed Identity (system- or user-assigned) on the host (App Service / VM / AKS) | Lets the app authenticate to Key Vault without storing any credential. |
| `DataProtectionKeys` table in the application database | Stores the (Key-Vault-wrapped) key ring itself. Created via a normal EF Core migration - `PersistKeysToDbContext` needs a durable store, Key Vault only wraps it. |

Key Vault protects the key; the database stores it. Neither one alone is sufficient.

## 1. Create the Key Vault and key

```powershell
az keyvault create --name <vaultName> --resource-group <resourceGroup> --location <location>
az keyvault key create --vault-name <vaultName> --name dataprotection-key --kty RSA --size 2048
```

Recommended: enable soft-delete and purge protection (enabled by default on new vaults) so an accidental key
deletion does not immediately and irrecoverably destroy the ability to decrypt the key ring (and therefore all
data encrypted through it).

> [!NOTE]
> New Key Vaults default to **RBAC authorization**. Creating the vault does **not** automatically grant you (or
> whoever runs these commands) permission to create keys in it - you need a role like **Key Vault Crypto Officer**
> assigned to yourself first (`az role assignment create --assignee <your-object-id> --role "Key Vault Crypto Officer" --scope <keyVaultResourceId>`),
> otherwise `az keyvault key create` fails with a `403 Forbidden` / `ForbiddenByRbac` error. Allow a minute or two
> for the role assignment to propagate before retrying.

## 2. Create a Managed Identity

A **user-assigned** Managed Identity is recommended over system-assigned, because it can be detached from one VM
and reattached to another without touching RBAC (see [VmMoveRunbook.md](VmMoveRunbook.md)).

```powershell
az identity create --name <identityName> --resource-group <resourceGroup>
```

Attach it to the host:

```powershell
az vm identity assign --name <vmName> --resource-group <resourceGroup> --identities <identityResourceId>
# or for App Service: az webapp identity assign --name <appName> --resource-group <resourceGroup> --identities <identityResourceId>
```

## 3. Grant RBAC roles

The identity needs exactly one role - no key management permissions, only usage:

- **Key Vault Crypto Service Encryption User** on the Key Vault (wrap/unwrap only).

```powershell
az role assignment create --assignee <identityPrincipalId> `
  --role "Key Vault Crypto Service Encryption User" `
  --scope <keyVaultResourceId>
```

No storage role is needed here because the key ring is persisted in the application's SQL database, not Blob
Storage - the app's existing SQL connection/credentials already cover that.

## 4. Application configuration

No secrets are required - authentication happens via the Managed Identity through `DefaultAzureCredential`.

`appsettings.json`:

```jsonc
{
  "DataProtection": {
    "KeyVaultKeyUri": "https://<vaultName>.vault.azure.net/keys/dataprotection-key",
    "ApplicationName": "MyApp"
  }
}
```

`Program.cs` (instead of `AddBlazorBaseDataProtection(...)`):

```csharp
builder.Services.AddBlazorBaseAzureKeyVaultDataProtection<ApplicationDbContext>(settings =>
{
    settings.ApplicationName = builder.Configuration["DataProtection:ApplicationName"]!;
    settings.AzureKeyVaultKeyIdentifier = new Uri(builder.Configuration["DataProtection:KeyVaultKeyUri"]!);
});
```

Do not include a specific key **version** in `KeyVaultKeyUri` - Data Protection then always uses the vault's
current key version automatically after a rotation.

## 5. EF Core migration

```csharp
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
}
```

```powershell
dotnet ef migrations add AddDataProtectionKeys
dotnet ef database update
```

## Availability and caching behaviour

- The unwrapped key ring is cached in the process for roughly 24 hours after a successful load - a brief Key
  Vault outage is normally not noticeable once the app has started successfully.
- Key Vault **must** be reachable at cold start (first request after a process restart/deployment/scale event),
  because the key ring has to be unwrapped before it can be used.
- Plan deployments and restarts accordingly (health checks, retries) and monitor Key Vault availability.

## Backup considerations

- The `DataProtectionKeys` table is part of the application database - your normal database backup already
  covers it.
- The Key Vault key itself must also be considered part of your disaster-recovery plan: if the key is permanently
  deleted (after the soft-delete retention period expires), the key ring - and therefore every value encrypted
  through it - becomes permanently unrecoverable.
