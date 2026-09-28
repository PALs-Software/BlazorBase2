#Requires -Modules Az.Accounts, Az.Compute, Az.Resources
<#
.SYNOPSIS
    Moves the Managed Identity used for Azure Key Vault Data Protection from one VM to another,
    without any data re-encryption (the key ring lives in the database, not on the VM).

.DESCRIPTION
    Use this after provisioning a new VM that will replace an existing one. Attaches the (existing,
    user-assigned) Managed Identity to the new VM and verifies it can obtain a Key Vault token via
    Azure Instance Metadata Service (IMDS) before you decommission the old VM.

    Run the verification step FROM THE NEW VM (e.g. via Invoke-AzVMRunCommand or a remote session) -
    IMDS is only reachable from within the VM itself.

.PARAMETER ResourceGroupName
    Resource group containing the VMs and the Managed Identity.

.PARAMETER IdentityResourceId
    Resource ID of the existing user-assigned Managed Identity
    (e.g. /subscriptions/.../resourceGroups/.../providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-myapp-dp).

.PARAMETER NewVmName
    Name of the new VM that should take over the identity.

.PARAMETER OldVmName
    Optional. Name of the old VM to detach the identity from. If omitted, the identity is only attached
    to the new VM (detaching manually / deleting the old VM will remove it there automatically).

.PARAMETER RemoveIdentityFromOldVm
    If set together with -OldVmName, explicitly removes the identity from the old VM instead of just
    leaving it in place until the VM is deleted.

.EXAMPLE
    ./Move-AzureKeyVaultDataProtectionIdentity.ps1 -ResourceGroupName rg-myapp `
        -IdentityResourceId /subscriptions/xxx/resourceGroups/rg-myapp/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-myapp-dp `
        -NewVmName vm-myapp-2 -OldVmName vm-myapp-1 -RemoveIdentityFromOldVm
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $ResourceGroupName,
    [Parameter(Mandatory = $true)] [string] $IdentityResourceId,
    [Parameter(Mandatory = $true)] [string] $NewVmName,
    [Parameter(Mandatory = $false)] [string] $OldVmName,
    [switch] $RemoveIdentityFromOldVm
)

$ErrorActionPreference = "Stop"

Write-Host "Attaching identity to new VM '$NewVmName' ..."
$newVm = Get-AzVM -ResourceGroupName $ResourceGroupName -Name $NewVmName
Update-AzVM -ResourceGroupName $ResourceGroupName -VM $newVm -IdentityType UserAssigned -IdentityId $IdentityResourceId | Out-Null

Write-Host ""
Write-Host "IMPORTANT: verify the new VM can obtain a Key Vault token BEFORE decommissioning the old VM."
Write-Host "Run the following FROM WITHIN the new VM (e.g. via Invoke-AzVMRunCommand or a remote session):"
Write-Host ""
Write-Host '  Invoke-RestMethod -Method Get -Uri "http://169.254.169.254/metadata/identity/oauth2/token?api-version=2019-08-01&resource=https://vault.azure.net" -Headers @{ Metadata = "true" }'
Write-Host ""
Write-Host "A successful response contains an 'access_token'. If it fails, wait a few minutes for RBAC/identity"
Write-Host "propagation and retry - do not proceed to decommission the old VM until this succeeds."

if ($OldVmName -and $RemoveIdentityFromOldVm) {
    $confirmation = Read-Host "Have you verified the NEW VM can obtain a Key Vault token? (yes/no)"
    if ($confirmation -ne "yes") {
        Write-Host "Aborting removal from old VM - verify the new VM first."
        return
    }

    Write-Host "Removing identity from old VM '$OldVmName' ..."
    $oldVm = Get-AzVM -ResourceGroupName $ResourceGroupName -Name $OldVmName
    Update-AzVM -ResourceGroupName $ResourceGroupName -VM $oldVm -IdentityType None | Out-Null
}

Write-Host ""
Write-Host "No application data needs to be re-encrypted - the Data Protection key ring is stored in the"
Write-Host "database and protected by the (unchanged) Key Vault key, independent of which VM runs the app."
