<#
.SYNOPSIS
    Configures Azure for GitHub Actions OIDC deployment.

.DESCRIPTION
    Idempotent. Creates (or reuses) a user-assigned managed identity, a federated credential
    trusting the GitHub repo's `main` branch, and grants the identity Contributor on the resource group.
    Prints the values you need to paste into GitHub repo variables.

.PARAMETER SubscriptionId
    Target Azure subscription ID.

.PARAMETER TenantId
    Tenant ID containing the subscription.

.PARAMETER ResourceGroup
    Resource group name (must already exist or this script creates it in -Location).

.PARAMETER Location
    Region for the RG (and the OIDC identity if RG doesn't exist yet).

.PARAMETER GithubRepo
    "owner/repo" of the GitHub repository (e.g. mb744/lasvegas-soccer-school).

.PARAMETER IdentityName
    Name of the user-assigned managed identity used for GitHub OIDC. Defaults to gh-deploy.

.EXAMPLE
    pwsh ./scripts/setup-azure-oidc.ps1 `
      -SubscriptionId f81bd820-6980-49b2-ab4b-1e1677695214 `
      -TenantId 8ad10099-6570-4225-834d-e5a9acdd7264 `
      -ResourceGroup soccer-school-west `
      -Location westus3 `
      -GithubRepo mb744/lasvegas-soccer-school
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $SubscriptionId,
    [Parameter(Mandatory)] [string] $TenantId,
    [Parameter(Mandatory)] [string] $ResourceGroup,
    [Parameter(Mandatory)] [string] $Location,
    [Parameter(Mandatory)] [string] $GithubRepo,
    [string] $IdentityName = 'gh-deploy'
)

$ErrorActionPreference = 'Stop'

function Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Info($msg) { Write-Host "    $msg" -ForegroundColor Gray }
function Ok($msg)   { Write-Host "    $msg" -ForegroundColor Green }

# ---- 0. Sanity checks ----
Step "Checking az login + subscription access"
az account set --subscription $SubscriptionId | Out-Null
$current = az account show --query "{sub:id, tenant:tenantId, user:user.name}" -o json | ConvertFrom-Json
if ($current.sub -ne $SubscriptionId) {
    throw "Active subscription is $($current.sub), expected $SubscriptionId. Run 'az login --tenant $TenantId' first."
}
if ($current.tenant -ne $TenantId) {
    throw "Active tenant is $($current.tenant), expected $TenantId."
}
Ok "Logged in as $($current.user) on sub $SubscriptionId"

# ---- 1. Resource group ----
Step "Ensuring resource group '$ResourceGroup' exists in '$Location'"
$rgExists = az group exists --name $ResourceGroup
if ($rgExists -ne 'true') {
    az group create --name $ResourceGroup --location $Location | Out-Null
    Ok "Created resource group"
} else {
    Ok "Resource group already exists"
}

# ---- 2. User-assigned managed identity for OIDC deployments ----
Step "Ensuring user-assigned managed identity '$IdentityName' exists"
$identity = az identity show --name $IdentityName --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json
if (-not $identity) {
    $identity = az identity create --name $IdentityName --resource-group $ResourceGroup --location $Location -o json | ConvertFrom-Json
    Ok "Created managed identity"
} else {
    Ok "Managed identity already exists"
}

$clientId = $identity.clientId
$principalId = $identity.principalId
Info "Client ID: $clientId"
Info "Principal ID: $principalId"

# ---- 3. Federated credentials trusting the GitHub repo ----
Step "Configuring federated credentials for $GithubRepo"

$federations = @(
    @{ Name = 'github-main';        Subject = "repo:${GithubRepo}:ref:refs/heads/main" },
    @{ Name = 'github-pull-request'; Subject = "repo:${GithubRepo}:pull_request" },
    @{ Name = 'github-environment-production'; Subject = "repo:${GithubRepo}:environment:production" }
)

foreach ($fed in $federations) {
    $existing = az identity federated-credential show `
        --name $fed.Name `
        --identity-name $IdentityName `
        --resource-group $ResourceGroup `
        -o json 2>$null
    if ($existing) {
        Info "  $($fed.Name) — already exists"
    } else {
        $params = @{
            name        = $fed.Name
            issuer      = 'https://token.actions.githubusercontent.com'
            subject     = $fed.Subject
            audiences   = @('api://AzureADTokenExchange')
            description = "GitHub Actions OIDC for $GithubRepo"
        } | ConvertTo-Json -Compress
        $params | Set-Content -Path "fed-temp.json" -Encoding utf8
        az identity federated-credential create `
            --identity-name $IdentityName `
            --resource-group $ResourceGroup `
            --parameters '@fed-temp.json' | Out-Null
        Remove-Item fed-temp.json -ErrorAction SilentlyContinue
        Ok "  Created $($fed.Name)"
    }
}

# ---- 4. Grant Contributor on the resource group ----
Step "Granting Contributor role on RG to managed identity"
$rgScope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup"
$existingRole = az role assignment list `
    --assignee-object-id $principalId `
    --assignee-principal-type ServicePrincipal `
    --scope $rgScope `
    --role Contributor `
    --query "[0].id" -o tsv 2>$null
if (-not $existingRole) {
    # MI may take a few seconds to propagate
    for ($i = 1; $i -le 6; $i++) {
        try {
            az role assignment create `
                --assignee-object-id $principalId `
                --assignee-principal-type ServicePrincipal `
                --role Contributor `
                --scope $rgScope | Out-Null
            Ok "Contributor granted"
            break
        } catch {
            if ($i -eq 6) { throw }
            Info "  Identity not yet propagated; retrying ($i)…"
            Start-Sleep -Seconds 5
        }
    }
} else {
    Ok "Contributor already granted"
}

# ---- 5. Output ----
$signedInUser = az ad signed-in-user show -o json | ConvertFrom-Json
$signedInObjectId = $signedInUser.id
$signedInUpn = $signedInUser.userPrincipalName ?? $signedInUser.mail ?? $signedInUser.displayName

Write-Host ""
Write-Host "================================================================" -ForegroundColor Green
Write-Host " ✓ Azure OIDC setup complete" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Set these as GitHub repo VARIABLES (Settings → Secrets and variables → Actions → Variables):"
Write-Host ""
Write-Host "  AZURE_CLIENT_ID            = $clientId"
Write-Host "  AZURE_TENANT_ID            = $TenantId"
Write-Host "  AZURE_SUBSCRIPTION_ID      = $SubscriptionId"
Write-Host "  AZURE_RESOURCE_GROUP       = $ResourceGroup"
Write-Host "  SQL_AAD_ADMIN_OBJECT_ID    = $signedInObjectId"
Write-Host "  SQL_AAD_ADMIN_LOGIN        = $signedInUpn"
Write-Host ""
Write-Host "Or run: gh variable set <NAME> --body '<value>' --repo $GithubRepo"
Write-Host ""
Write-Host "All values above are non-secret; the federated credential means there is no client secret to store."
