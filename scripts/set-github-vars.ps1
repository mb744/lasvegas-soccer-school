<#
.SYNOPSIS
    Sets the GitHub repository variables required by the deploy workflow.

.EXAMPLE
    pwsh ./scripts/set-github-vars.ps1 `
      -Repo mb744/lasvegas-soccer-school `
      -ClientId 11111111-1111-1111-1111-111111111111 `
      -TenantId 8ad10099-6570-4225-834d-e5a9acdd7264 `
      -SubscriptionId f81bd820-6980-49b2-ab4b-1e1677695214 `
      -ResourceGroup soccer-school-west `
      -SqlAdminObjectId 99999999-9999-9999-9999-999999999999 `
      -SqlAdminLogin "live.com#mbaez744@gmail.com"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Repo,
    [Parameter(Mandatory)] [string] $ClientId,
    [Parameter(Mandatory)] [string] $TenantId,
    [Parameter(Mandatory)] [string] $SubscriptionId,
    [Parameter(Mandatory)] [string] $ResourceGroup,
    [Parameter(Mandatory)] [string] $SqlAdminObjectId,
    [Parameter(Mandatory)] [string] $SqlAdminLogin
)

$ErrorActionPreference = 'Stop'

$vars = [ordered]@{
    AZURE_CLIENT_ID         = $ClientId
    AZURE_TENANT_ID         = $TenantId
    AZURE_SUBSCRIPTION_ID   = $SubscriptionId
    AZURE_RESOURCE_GROUP    = $ResourceGroup
    SQL_AAD_ADMIN_OBJECT_ID = $SqlAdminObjectId
    SQL_AAD_ADMIN_LOGIN     = $SqlAdminLogin
}

foreach ($name in $vars.Keys) {
    Write-Host "Setting $name..." -ForegroundColor Cyan
    gh variable set $name --body $vars[$name] --repo $Repo
}

Write-Host ""
Write-Host "[OK] All variables set. Verify with: gh variable list --repo $Repo" -ForegroundColor Green
