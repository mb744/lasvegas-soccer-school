// Las Vegas Soccer School — main infrastructure deployment.
// Scope: resource group (existing).
// Run from GitHub Actions via `az deployment group create`.

targetScope = 'resourceGroup'

@description('Short application name used as a prefix for resource names.')
param appName string = 'lvss'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Container image (full reference, e.g. ghcr.io/owner/repo:sha-abc1234).')
param containerImage string

@description('Object ID of the user (or group) to set as Microsoft Entra admin on the SQL server. Used by humans for break-glass access.')
param sqlAadAdminObjectId string

@description('UPN or display name of the SQL Entra admin (display label only).')
param sqlAadAdminLogin string

@description('Tag value applied to all resources.')
param tagEnvironment string = 'production'

// ----- Naming -----
var nameSuffix = uniqueString(resourceGroup().id)
var logAnalyticsName = '${appName}-logs-${nameSuffix}'
var managedIdentityName = '${appName}-id-${nameSuffix}'
var containerAppEnvName = '${appName}-cae-${nameSuffix}'
var containerAppName = '${appName}-app'
var sqlServerName = '${appName}-sql-${nameSuffix}'
var sqlDatabaseName = 'LasVegasSoccerSchool'

var commonTags = {
  app: appName
  environment: tagEnvironment
}

// ----- Modules -----
module logAnalytics 'modules/log-analytics.bicep' = {
  name: 'logAnalytics'
  params: {
    name: logAnalyticsName
    location: location
    tags: commonTags
  }
}

module managedIdentity 'modules/managed-identity.bicep' = {
  name: 'managedIdentity'
  params: {
    name: managedIdentityName
    location: location
    tags: commonTags
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    serverName: sqlServerName
    databaseName: sqlDatabaseName
    location: location
    tags: commonTags
    aadAdminLogin: sqlAadAdminLogin
    aadAdminObjectId: sqlAadAdminObjectId
    appIdentityName: managedIdentity.outputs.name
    appIdentityPrincipalId: managedIdentity.outputs.principalId
    appIdentityClientId: managedIdentity.outputs.clientId
  }
}

module containerEnv 'modules/container-apps-env.bicep' = {
  name: 'containerEnv'
  params: {
    name: containerAppEnvName
    location: location
    tags: commonTags
    logAnalyticsCustomerId: logAnalytics.outputs.customerId
    logAnalyticsSharedKey: logAnalytics.outputs.primarySharedKey
  }
}

module containerApp 'modules/container-app.bicep' = {
  name: 'containerApp'
  params: {
    name: containerAppName
    location: location
    tags: commonTags
    environmentId: containerEnv.outputs.id
    image: containerImage
    managedIdentityResourceId: managedIdentity.outputs.id
    managedIdentityClientId: managedIdentity.outputs.clientId
    sqlConnectionString: sql.outputs.connectionString
    adminApiKey: guid(resourceGroup().id, 'admin-api-key')
  }
}

// ----- Outputs -----
output appUrl string = 'https://${containerApp.outputs.fqdn}'
output containerAppName string = containerApp.outputs.name
output sqlServerFqdn string = sql.outputs.serverFqdn
output managedIdentityClientId string = managedIdentity.outputs.clientId
