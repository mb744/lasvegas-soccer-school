// Azure SQL Server + serverless free-tier database, configured for Entra-only auth.
// The user-assigned managed identity is set as the Microsoft Entra admin so the app
// has full DDL/DML rights on first connect (sufficient for EF Core migrations).
// A human admin object ID is also set for break-glass access via SSMS / azdata.

param serverName string
param databaseName string
param location string
param tags object = {}

@description('Object ID of the human Entra admin (e.g. you).')
param aadAdminObjectId string

@description('UPN or display label for the human Entra admin.')
param aadAdminLogin string

@description('Name of the user-assigned managed identity that the app uses to connect. Granted DB access by scripts/grant-mi-db-access.ps1 after first deployment.')
param appIdentityName string

@description('Reserved for future use. Kept for symmetry with the main module wiring.')
param appIdentityPrincipalId string = ''

@description('Reserved for future use.')
param appIdentityClientId string = ''

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  tags: tags
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    // Microsoft Entra-only authentication (no SQL logins).
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'User'
      login: aadAdminLogin
      sid: aadAdminObjectId
      tenantId: tenant().tenantId
      azureADOnlyAuthentication: true
    }
    restrictOutboundNetworkAccess: 'Disabled'
  }
}

// Allow other Azure services (incl. Container Apps) to reach the SQL server.
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  name: 'AllowAllAzureServices'
  parent: sqlServer
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Free serverless General Purpose tier — auto-pauses, 32 GB, ~100k vCore-sec/mo free.
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  name: databaseName
  parent: sqlServer
  location: location
  tags: tags
  sku: {
    name: 'GP_S_Gen5_2'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 2
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    catalogCollation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: false
    minCapacity: json('0.5')
    autoPauseDelay: 60
    maxSizeBytes: 34359738368  // 32 GB
    requestedBackupStorageRedundancy: 'Local'
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
  }
}

output serverName string = sqlServer.name
output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = sqlDatabase.name
output appIdentityName string = appIdentityName
output _unused string = '${appIdentityPrincipalId}${appIdentityClientId}'  // suppress unused-param warnings; values flow from main.bicep but are applied via post-deploy script.

// Connection string used by the app — DefaultAzureCredential picks up the user-assigned
// managed identity via the AZURE_CLIENT_ID env var set on the Container App.
output connectionString string = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabase.name};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
