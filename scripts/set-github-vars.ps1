<#
.SYNOPSIS
    Sets the GitHub repository variables and secrets required by the deploy workflow.

.DESCRIPTION
    Sets non-sensitive values as repo VARIABLES and sensitive values as repo SECRETS.
    Skips any optional value passed as empty/null. Idempotent.

    Variables:  AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_SUBSCRIPTION_ID, AZURE_RESOURCE_GROUP,
                SQL_AAD_ADMIN_OBJECT_ID, SQL_AAD_ADMIN_LOGIN,
                ACTIVE_SEASON (optional), CUSTOM_DOMAIN (optional),
                ADMIN_BOOTSTRAP_EMAIL (optional),
                GOOGLE_OAUTH_CLIENT_ID (optional), FACEBOOK_OAUTH_APP_ID (optional),
                ENABLE_ACS (optional, 'true'/'false'), ACS_SMS_FROM_NUMBER (optional, E.164)

    Secrets:    ADMIN_BOOTSTRAP_PASSWORD (optional),
                GOOGLE_OAUTH_CLIENT_SECRET (optional), FACEBOOK_OAUTH_APP_SECRET (optional)

.EXAMPLE
    pwsh ./scripts/set-github-vars.ps1 `
      -Repo mb744/lasvegas-soccer-school `
      -ClientId 11111111-1111-1111-1111-111111111111 `
      -TenantId 8ad10099-6570-4225-834d-e5a9acdd7264 `
      -SubscriptionId f81bd820-6980-49b2-ab4b-1e1677695214 `
      -ResourceGroup soccer-school-west `
      -SqlAdminObjectId 99999999-9999-9999-9999-999999999999 `
      -SqlAdminLogin "live.com#mbaez744@gmail.com" `
      -AdminBootstrapEmail "you@example.com" `
      -AdminBootstrapPassword "Some-strong-temp-pwd-1!" `
      -GoogleClientId "xxx.apps.googleusercontent.com" `
      -GoogleClientSecret "GOCSPX-xxx" `
      -FacebookAppId "1234567890" `
      -FacebookAppSecret "abcdef1234"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Repo,
    [Parameter(Mandatory)] [string] $ClientId,
    [Parameter(Mandatory)] [string] $TenantId,
    [Parameter(Mandatory)] [string] $SubscriptionId,
    [Parameter(Mandatory)] [string] $ResourceGroup,
    [Parameter(Mandatory)] [string] $SqlAdminObjectId,
    [Parameter(Mandatory)] [string] $SqlAdminLogin,

    # Optional season + custom domain + admin bootstrap + OAuth + ACS
    [string] $ActiveSeason,
    [string] $CustomDomain,
    [string] $AdminBootstrapEmail,
    [string] $AdminBootstrapPassword,
    [string] $GoogleClientId,
    [string] $GoogleClientSecret,
    [string] $FacebookAppId,
    [string] $FacebookAppSecret,
    [ValidateSet('true', 'false', '')] [string] $EnableAcs,
    [string] $AcsSmsFromNumber
)

$ErrorActionPreference = 'Stop'

$vars = [ordered]@{
    AZURE_CLIENT_ID         = $ClientId
    AZURE_TENANT_ID         = $TenantId
    AZURE_SUBSCRIPTION_ID   = $SubscriptionId
    AZURE_RESOURCE_GROUP    = $ResourceGroup
    SQL_AAD_ADMIN_OBJECT_ID = $SqlAdminObjectId
    SQL_AAD_ADMIN_LOGIN     = $SqlAdminLogin
    ACTIVE_SEASON           = $ActiveSeason
    CUSTOM_DOMAIN           = $CustomDomain
    ADMIN_BOOTSTRAP_EMAIL   = $AdminBootstrapEmail
    GOOGLE_OAUTH_CLIENT_ID  = $GoogleClientId
    FACEBOOK_OAUTH_APP_ID   = $FacebookAppId
    ENABLE_ACS              = $EnableAcs
    ACS_SMS_FROM_NUMBER     = $AcsSmsFromNumber
}

foreach ($name in $vars.Keys) {
    $val = $vars[$name]
    if ([string]::IsNullOrWhiteSpace($val)) {
        Write-Host "Skipping $name (not provided)..." -ForegroundColor DarkGray
        continue
    }
    Write-Host "Setting variable $name..." -ForegroundColor Cyan
    gh variable set $name --body $val --repo $Repo
}

$secrets = [ordered]@{
    ADMIN_BOOTSTRAP_PASSWORD   = $AdminBootstrapPassword
    GOOGLE_OAUTH_CLIENT_SECRET = $GoogleClientSecret
    FACEBOOK_OAUTH_APP_SECRET  = $FacebookAppSecret
}

foreach ($name in $secrets.Keys) {
    $val = $secrets[$name]
    if ([string]::IsNullOrWhiteSpace($val)) {
        Write-Host "Skipping secret $name (not provided)..." -ForegroundColor DarkGray
        continue
    }
    Write-Host "Setting secret $name..." -ForegroundColor Cyan
    gh secret set $name --body $val --repo $Repo
}

Write-Host ""
Write-Host "[OK] Done. Verify with:" -ForegroundColor Green
Write-Host "  gh variable list --repo $Repo"
Write-Host "  gh secret list   --repo $Repo"
