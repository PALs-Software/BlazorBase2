#Requires -Modules Az.Accounts, Az.KeyVault, Az.Resources, Az.ManagedServiceIdentity
<#
.SYNOPSIS
    Provisions the Azure resources required for AddBlazorBaseAzureKeyVaultDataProtection<TDbContext>:
    a Key Vault with an RSA key, a user-assigned Managed Identity, and the RBAC role assignment
    connecting the two. Optionally attaches the identity to an existing VM.

.DESCRIPTION
    This script is idempotent-ish: it uses "New-*" cmdlets that fail if the resource already exists.
    Re-run individual sections manually if you only need to redo part of the setup.

.PARAMETER ResourceGroupName
    Resource group that will contain the Key Vault and the Managed Identity.

.PARAMETER Location
    Azure region, e.g. "westeurope".

.PARAMETER KeyVaultName
    Globally unique Key Vault name.

.PARAMETER KeyName
    Name of the RSA key created inside the vault. Defaults to "dataprotection-key".

.PARAMETER IdentityName
    Optional. Name of the user-assigned Managed Identity to create. Only needed when the app will actually run on
    a VM/App Service using this identity to authenticate against Key Vault - omit it for pure local/dev testing,
    where you authenticate as yourself instead (see Documentation/AzureKeyVaultSetup.md).

.PARAMETER VmName
    Optional. If given (requires -IdentityName), the created identity is attached to this existing VM (must be in
    the same resource group).

.EXAMPLE
    ./Setup-AzureKeyVaultDataProtection.ps1 -ResourceGroupName rg-myapp -Location westeurope `
        -KeyVaultName kv-myapp-dp -IdentityName id-myapp-dp -VmName vm-myapp

.EXAMPLE
    # Local/dev testing only - no Managed Identity needed, you authenticate as yourself
    ./Setup-AzureKeyVaultDataProtection.ps1 -ResourceGroupName rg-dev-test -Location westeurope -KeyVaultName kv-dev-test
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $ResourceGroupName,
    [Parameter(Mandatory = $true)] [string] $Location,
    [Parameter(Mandatory = $true)] [string] $KeyVaultName,
    [Parameter(Mandatory = $false)] [string] $KeyName = "dataprotection-key",
    [Parameter(Mandatory = $false)] [string] $IdentityName,
    [Parameter(Mandatory = $false)] [string] $VmName
)

$ErrorActionPreference = "Stop"

if ($VmName -and -not $IdentityName) {
    throw "-VmName requires -IdentityName to also be specified."
}

$vault = Get-AzKeyVault -VaultName $KeyVaultName -ResourceGroupName $ResourceGroupName -ErrorAction SilentlyContinue
if ($vault) {
    Write-Host "Key Vault '$KeyVaultName' already exists, reusing it ..."
}
else {
    Write-Host "Creating Key Vault '$KeyVaultName' in '$ResourceGroupName' / '$Location' ..."
    # Newer Az.KeyVault versions (6.x+) default new vaults to RBAC authorization and always-on soft-delete,
    # so no explicit -EnableRbacAuthorization/-EnableSoftDelete switches are needed (or available) anymore.
    $vault = New-AzKeyVault -Name $KeyVaultName -ResourceGroupName $ResourceGroupName -Location $Location `
        -EnablePurgeProtection
}

# With RBAC-based vaults, creating the vault does NOT grant the caller permission to manage keys in it - you need
# a role like "Key Vault Crypto Officer" assigned to yourself first (see README/AzureKeyVaultSetup.md).
Write-Host "Creating RSA key '$KeyName' ..."
$key = Add-AzKeyVaultKey -VaultName $KeyVaultName -Name $KeyName -Destination Software -KeyType RSA -Size 2048
Write-Host "Key identifier (use as DataProtection:KeyVaultKeyUri, WITHOUT the version): $($key.Id)"

if ($IdentityName) {
    Write-Host "Creating user-assigned Managed Identity '$IdentityName' ..."
    $identity = New-AzUserAssignedIdentity -ResourceGroupName $ResourceGroupName -Name $IdentityName -Location $Location

    Write-Host "Assigning 'Key Vault Crypto Service Encryption User' role to the identity on the vault ..."
    $existingAssignment = Get-AzRoleAssignment -ObjectId $identity.PrincipalId -RoleDefinitionName "Key Vault Crypto Service Encryption User" -Scope $vault.ResourceId -ErrorAction SilentlyContinue
    if ($existingAssignment) {
        Write-Host "Role assignment already exists, skipping."
    }
    else {
        New-AzRoleAssignment -ObjectId $identity.PrincipalId -RoleDefinitionName "Key Vault Crypto Service Encryption User" `
            -Scope $vault.ResourceId | Out-Null
    }

    if ($VmName) {
        Write-Host "Attaching identity to VM '$VmName' ..."
        $vm = Get-AzVM -ResourceGroupName $ResourceGroupName -Name $VmName
        Update-AzVM -ResourceGroupName $ResourceGroupName -VM $vm -IdentityType UserAssigned -IdentityId $identity.Id | Out-Null
    }
}
else {
    Write-Host "No -IdentityName given - skipping Managed Identity creation (local/dev testing only: grant yourself"
    Write-Host "'Key Vault Crypto Service Encryption User' on the vault instead, see Documentation/AzureKeyVaultSetup.md)."
}

Write-Host ""
Write-Host "Done. appsettings.json values:"
Write-Host "  DataProtection:KeyVaultKeyUri = $($key.Id -replace '/[0-9a-f]{32}$', '')"
Write-Host "  DataProtection:ApplicationName = <choose a stable, unique application name>"
Write-Host ""
Write-Host "Remember to also add and apply the EF Core migration for IDataProtectionKeyContext (DataProtectionKeys table)."
