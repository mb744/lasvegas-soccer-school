<#
.SYNOPSIS
    Grants the app's user-assigned managed identity db_owner on the Azure SQL database.

.DESCRIPTION
    Run this ONCE after the first deployment. The Azure SQL server has Entra-only
    auth and you (the human admin) are the AAD admin; this script signs in as you
    and creates a database user for the Container App's managed identity.

    Idempotent - safe to re-run.

.PARAMETER SqlServerFqdn
    The full DNS name of the Azure SQL server, e.g. lvss-sql-xxxx.database.windows.net.
    Get from `gh variable get`/Bicep outputs or the deploy workflow's "Show deployment outputs" step.

.PARAMETER DatabaseName
    Name of the database. Default LasVegasSoccerSchool.

.PARAMETER ManagedIdentityName
    Name of the user-assigned managed identity created by the Bicep deployment
    (typically `lvss-id-<suffix>`).

.EXAMPLE
    pwsh ./scripts/grant-mi-db-access.ps1 `
      -SqlServerFqdn lvss-sql-abcdef.database.windows.net `
      -ManagedIdentityName lvss-id-abcdef
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $SqlServerFqdn,
    [Parameter(Mandatory)] [string] $ManagedIdentityName,
    [string] $DatabaseName = 'LasVegasSoccerSchool'
)

$ErrorActionPreference = 'Stop'

# Need sqlcmd. On Windows it ships with the SQL Server tools / SSMS.
$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd) {
    Write-Host "sqlcmd not found. Install via:" -ForegroundColor Yellow
    Write-Host "  winget install Microsoft.SqlCmd" -ForegroundColor Yellow
    Write-Host "Or use the Azure Portal Query Editor and paste the T-SQL below:" -ForegroundColor Yellow
    Write-Host ""
}

$sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '$ManagedIdentityName')
BEGIN
    CREATE USER [$ManagedIdentityName] FROM EXTERNAL PROVIDER;
END;
ALTER ROLE db_owner ADD MEMBER [$ManagedIdentityName];
SELECT 'Granted db_owner to $ManagedIdentityName' AS result;
"@

Write-Host "T-SQL to run on database '$DatabaseName' on server '$SqlServerFqdn':" -ForegroundColor Cyan
Write-Host ""
Write-Host $sql -ForegroundColor Gray
Write-Host ""

if ($sqlcmd) {
    Write-Host "==> Running via sqlcmd with Azure AD interactive auth..." -ForegroundColor Cyan
    # -G alone works for both classic Microsoft sqlcmd (>= ~2018) and go-sqlcmd; both pick
    # up Azure AD interactive auth and pop a browser. Avoid --authentication-method which is
    # go-sqlcmd-only and breaks classic sqlcmd parsing.
    & sqlcmd -S $SqlServerFqdn -d $DatabaseName -G -Q $sql
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "sqlcmd exited with code $LASTEXITCODE." -ForegroundColor Yellow
        Write-Host "Fall back to the Azure Portal Query editor:" -ForegroundColor Yellow
        Write-Host "  Portal -> SQL databases -> $DatabaseName -> Query editor (preview)" -ForegroundColor Yellow
        Write-Host "  Authenticate with Active Directory, then paste the T-SQL above." -ForegroundColor Yellow
        throw "sqlcmd failed with exit code $LASTEXITCODE"
    }
    Write-Host "[OK] Done." -ForegroundColor Green
} else {
    Write-Host "Copy the T-SQL above and run it via Portal -> SQL Database -> Query editor (signed in as your Entra admin user)." -ForegroundColor Yellow
}
