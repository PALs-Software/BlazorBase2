# Moving the application to a new VM

Because the Data Protection key ring is persisted in the application database (not on the VM's disk) and is
wrapped by an Azure Key Vault key (not by anything machine-specific), moving the application to a new VM requires
**no re-encryption of any application data**. Only the Managed Identity used to authenticate to Key Vault needs to
be reassigned.

## If you used a user-assigned Managed Identity (recommended)

The identity is an independent Azure resource - simply detach it from the old VM and attach it to the new one.
RBAC role assignments stay untouched because they are bound to the identity, not to the VM.

```powershell
az vm identity assign --name <newVmName> --resource-group <resourceGroup> --identities <identityResourceId>
```

Then verify the new VM can obtain a token before decommissioning the old one (see
[Move-AzureKeyVaultDataProtectionIdentity.ps1](../Scripts/Move-AzureKeyVaultDataProtectionIdentity.ps1)).

Only once the new VM is confirmed working, delete the old VM:

```powershell
az vm delete --name <oldVmName> --resource-group <resourceGroup>
```

## If you used a system-assigned Managed Identity

System-assigned identities are deleted automatically when their VM is deleted, along with their RBAC role
assignments. On the new VM:

1. Enable a new system-assigned identity: `az vm identity assign --name <newVmName> --resource-group <resourceGroup>`
2. Re-create the RBAC role assignment with the **new** identity's principal ID (see
   [AzureKeyVaultSetup.md](AzureKeyVaultSetup.md) step 3).
3. Verify the new VM can obtain a token (see script below).
4. Only then delete the old VM.

## Verification

From the new VM, confirm the Instance Metadata Service (IMDS) returns a token for Key Vault:

```powershell
Invoke-RestMethod -Method Get -Uri "http://169.254.169.254/metadata/identity/oauth2/token?api-version=2019-08-01&resource=https://vault.azure.net" -Headers @{ Metadata = "true" }
```

A successful response contains an `access_token`. If this fails, do not decommission the old VM yet - check that
the identity is attached and the RBAC role assignment exists and has propagated (can take a few minutes).

## Checklist

- [ ] New VM has the Managed Identity attached (user-assigned: reused; system-assigned: newly created)
- [ ] RBAC role assignment exists for the (possibly new) identity's principal ID
- [ ] IMDS token request from the new VM succeeds
- [ ] Application on the new VM starts successfully and can read/write encrypted values
- [ ] `appsettings.json` (`DataProtection:KeyVaultKeyUri`, `ApplicationName`) is unchanged/carried over
- [ ] Only after the above is confirmed: decommission the old VM
- [ ] No data migration or re-encryption step is required - the key ring and Key Vault key did not move
