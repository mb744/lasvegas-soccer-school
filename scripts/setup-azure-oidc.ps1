<#
.SYNOPSIS
    Configures Azure for GitHub Actions OIDC deployment.

.DESCRIPTION
    Idempotent. Creates (or reuses) a user-assigned managed identity, a federated credential
    trusting the GitHub repo's `main` branch, and grants the identity Contributor on the resource group.
    Prints the values you need to paste into GitHub repo variables.

    All Azure CLI calls retry on transient network errors. If a call fails non-transiently,
    re-run the script - every step is idempotent.

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
    # Works on Windows PowerShell 5.1 and PowerShell 7
    powershell -File ./scripts/setup-azure-oidc.ps1 `
      -SubscriptionId f81bd820-6980-49b2-ab4b-1e1677695214 `
      -TenantId 8ad10099-6570-4225-834d-e5a9acdd7264 `
      -ResourceGroup soccer-school-west `
      -Location westus `
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
function Warn($msg) { Write-Host "    $msg" -ForegroundColor Yellow }

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "az CLI not found on PATH."
}

# Run an az command via the PowerShell call operator (splatting preserves arg boundaries
# including those containing spaces, e.g. JMESPath queries). Returns stdout text on success,
# $null if -AllowMissing and the resource doesn't exist. Retries on transient network errors.
function Invoke-Az {
    param(
        [Parameter(Mandatory)] [string[]] $AzArgs,
        [switch] $AllowMissing,
        [int] $MaxAttempts = 4
    )

    # Local override: in PS 5.1, native-command stderr captured via 2>&1 produces
    # ErrorRecord objects that the parent's $ErrorActionPreference='Stop' would
    # turn into terminating errors. We want to inspect them, not crash on them.
    $ErrorActionPreference = 'Continue'

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        $merged = & az @AzArgs 2>&1
        $exitCode = $LASTEXITCODE

        $stdoutLines = New-Object System.Collections.Generic.List[string]
        $stderrLines = New-Object System.Collections.Generic.List[string]
        foreach ($line in $merged) {
            if ($line -is [System.Management.Automation.ErrorRecord]) {
                [void]$stderrLines.Add([string]$line)
            } else {
                [void]$stdoutLines.Add([string]$line)
            }
        }
        $stdout = $stdoutLines -join "`n"
        $stderr = $stderrLines -join "`n"
        $allText = "$stdout`n$stderr"

        if ($exitCode -eq 0) { return $stdout }

        if ($AllowMissing -and ($allText -match '(?i)ResourceNotFound|was not found|cannot be found|does not exist|Not Found')) {
            return $null
        }

        $isTransient = $allText -match '(?i)(Connection aborted|10054|RemoteDisconnected|read timed out|timed? ?out|temporarily unavailable|HTTPSConnectionPool|\b503\b|\b429\b|\b502\b|\b504\b|Bad Gateway)'
        if ($isTransient -and $attempt -lt $MaxAttempts) {
            $delay = 5 * $attempt
            Warn "[transient network error] retrying in $delay s (attempt $attempt of $MaxAttempts)"
            Start-Sleep -Seconds $delay
            continue
        }

        throw "az $($AzArgs -join ' ') failed (exit $exitCode):`n$allText"
    }
    throw "az $($AzArgs -join ' ') failed after $MaxAttempts attempts (transient errors)."
}

# ---- 0. Sanity checks ----
Step "Checking az login + subscription access"
Invoke-Az -AzArgs @('account','set','--subscription',$SubscriptionId) | Out-Null
$accountJson = Invoke-Az -AzArgs @('account','show','--query','{sub:id,tenant:tenantId,user:user.name}','-o','json')
$current = $accountJson | ConvertFrom-Json
if ($current.sub -ne $SubscriptionId) {
    throw "Active subscription is $($current.sub), expected $SubscriptionId. Run 'az login --tenant $TenantId' first."
}
if ($current.tenant -ne $TenantId) {
    throw "Active tenant is $($current.tenant), expected $TenantId."
}
Ok "Logged in as $($current.user) on sub $SubscriptionId"

# ---- 1. Resource group ----
Step "Ensuring resource group '$ResourceGroup' exists in '$Location'"
$rgExists = (Invoke-Az -AzArgs @('group','exists','--name',$ResourceGroup)).Trim()
if ($rgExists -ne 'true') {
    Invoke-Az -AzArgs @('group','create','--name',$ResourceGroup,'--location',$Location) | Out-Null
    Ok "Created resource group"
} else {
    Ok "Resource group already exists"
}

# ---- 2. User-assigned managed identity for OIDC deployments ----
Step "Ensuring user-assigned managed identity '$IdentityName' exists"
$identityJson = Invoke-Az -AzArgs @('identity','show','--name',$IdentityName,'--resource-group',$ResourceGroup,'--output','json') -AllowMissing
if (-not $identityJson) {
    $identityJson = Invoke-Az -AzArgs @('identity','create','--name',$IdentityName,'--resource-group',$ResourceGroup,'--location',$Location,'-o','json')
    Ok "Created managed identity"
} else {
    Ok "Managed identity already exists"
}
$identity = $identityJson | ConvertFrom-Json
$clientId = $identity.clientId
$principalId = $identity.principalId
Info "Client ID: $clientId"
Info "Principal ID: $principalId"

# ---- 3. Federated credentials trusting the GitHub repo ----
Step "Configuring federated credentials for $GithubRepo"

$federations = @(
    @{ Name = 'github-main';                   Subject = ('repo:' + $GithubRepo + ':ref:refs/heads/main') },
    @{ Name = 'github-pull-request';           Subject = ('repo:' + $GithubRepo + ':pull_request') },
    @{ Name = 'github-environment-production'; Subject = ('repo:' + $GithubRepo + ':environment:production') }
)

foreach ($fed in $federations) {
    $existing = Invoke-Az -AzArgs @('identity','federated-credential','show','--name',$fed.Name,'--identity-name',$IdentityName,'--resource-group',$ResourceGroup,'-o','json') -AllowMissing
    if ($existing) {
        Info ("  " + $fed.Name + " - already exists")
    } else {
        $params = @{
            name        = $fed.Name
            issuer      = 'https://token.actions.githubusercontent.com'
            subject     = $fed.Subject
            audiences   = @('api://AzureADTokenExchange')
            description = "GitHub Actions OIDC for $GithubRepo"
        } | ConvertTo-Json -Compress
        $tmp = [System.IO.Path]::GetTempFileName()
        try {
            # Write as UTF-8 without BOM so az parses it correctly
            [System.IO.File]::WriteAllText($tmp, $params, (New-Object System.Text.UTF8Encoding $false))
            Invoke-Az -AzArgs @('identity','federated-credential','create','--identity-name',$IdentityName,'--resource-group',$ResourceGroup,'--parameters',('@' + $tmp)) | Out-Null
            Ok ("  Created " + $fed.Name)
        } finally {
            Remove-Item $tmp -Force -ErrorAction SilentlyContinue
        }
    }
}

# ---- 4. Grant Contributor on the resource group ----
Step "Granting Contributor role on RG to managed identity"
$rgScope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup"
$existingRoleJson = Invoke-Az -AzArgs @(
    'role','assignment','list',
    '--assignee-object-id',$principalId,
    '--assignee-principal-type','ServicePrincipal',
    '--scope',$rgScope,
    '--role','Contributor',
    '--query','[0].id','-o','tsv'
)
$existingRole = $existingRoleJson.Trim()
if (-not $existingRole) {
    $assigned = $false
    for ($i = 1; $i -le 6; $i++) {
        try {
            Invoke-Az -AzArgs @(
                'role','assignment','create',
                '--assignee-object-id',$principalId,
                '--assignee-principal-type','ServicePrincipal',
                '--role','Contributor',
                '--scope',$rgScope
            ) | Out-Null
            $assigned = $true
            break
        } catch {
            if ($i -eq 6) { throw }
            Info "  Identity not yet propagated; retrying ($i)..."
            Start-Sleep -Seconds 5
        }
    }
    if ($assigned) { Ok "Contributor granted" }
} else {
    Ok "Contributor already granted"
}

# ---- 5. Output ----
$signedInJson = Invoke-Az -AzArgs @('ad','signed-in-user','show','-o','json')
$signedInUser = $signedInJson | ConvertFrom-Json
$signedInObjectId = $signedInUser.id
$signedInUpn = $signedInUser.userPrincipalName
if (-not $signedInUpn) { $signedInUpn = $signedInUser.mail }
if (-not $signedInUpn) { $signedInUpn = $signedInUser.displayName }

Write-Host ""
Write-Host "================================================================" -ForegroundColor Green
Write-Host " [OK] Azure OIDC setup complete" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Set these as GitHub repo VARIABLES (Settings -> Secrets and variables -> Actions -> Variables):"
Write-Host ""
Write-Host "  AZURE_CLIENT_ID            = $clientId"
Write-Host "  AZURE_TENANT_ID            = $TenantId"
Write-Host "  AZURE_SUBSCRIPTION_ID      = $SubscriptionId"
Write-Host "  AZURE_RESOURCE_GROUP       = $ResourceGroup"
Write-Host "  SQL_AAD_ADMIN_OBJECT_ID    = $signedInObjectId"
Write-Host "  SQL_AAD_ADMIN_LOGIN        = $signedInUpn"
Write-Host ""
Write-Host "Or run scripts/set-github-vars.ps1 with these values."
Write-Host ""
Write-Host "All values above are non-secret; the federated credential means there is no client secret to store."
