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

@description('Active season label stamped on new registrations, e.g. "2026/27".')
param activeSeason string = '2026/27'

@description('Optional custom domain bound to the Container App (e.g. registration.lasvegassoccerschool.org). When set, the app generates outreach links and OAuth redirect URIs against this host instead of the auto-generated Container Apps FQDN. The hostname binding itself is one-time, done via `az containerapp hostname add/bind` after DNS is configured.')
param customDomain string = ''

@description('When true, provisions Azure Communication Services (ACS) + Email Communication Service + Azure-managed email domain. Wired into the container app for email outreach. SMS phone numbers must still be purchased separately and provided via acsSmsFromNumber.')
param enableAcs bool = false

@description('Sender phone number for ACS SMS in E.164 format (e.g. +18005551212). Purchase the number in the Azure portal first; ACS does not expose phone-number purchase to Bicep. Empty disables SMS outreach.')
param acsSmsFromNumber string = ''

@description('Optional customer-managed email domain already verified in the Email Communication Service (e.g. "lasvegassoccerschool.org"). When set, outreach emails send from <acsCustomEmailLocalPart>@<acsCustomEmailDomain> instead of the Azure-managed *.azurecomm.net address.')
param acsCustomEmailDomain string = ''

@description('Local-part of the sender on the custom email domain. Must already exist as a Sender Username on that domain (created via the Azure portal).')
param acsCustomEmailLocalPart string = 'registration'

@description('Twilio Account SID. When provided alongside auth token + from number, OutreachSender uses Twilio for SMS instead of ACS.')
param twilioAccountSid string = ''

@secure()
@description('Twilio auth token.')
param twilioAuthToken string = ''

@description('Twilio sender phone number in E.164 format (e.g. +18005551212). Must be a Twilio-owned number.')
param twilioSmsFromNumber string = ''

@description('Email of the bootstrap admin Identity user. Created (with Admin role) on first start. Leave empty to skip bootstrap.')
param adminBootstrapEmail string = ''

@secure()
@description('Initial password for the bootstrap admin. Used only at create time; ignored if user already exists.')
param adminBootstrapPassword string = ''

@description('Google OAuth Client ID. Leave empty to disable Google login.')
param googleOAuthClientId string = ''

@secure()
param googleOAuthClientSecret string = ''

@description('Facebook OAuth App ID. Leave empty to disable Facebook login.')
param facebookOAuthAppId string = ''

@secure()
param facebookOAuthAppSecret string = ''

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

module acs 'modules/acs.bicep' = if (enableAcs) {
  name: 'acs'
  params: {
    appName: appName
    tags: commonTags
    customEmailDomain: acsCustomEmailDomain
    customSenderLocalPart: acsCustomEmailLocalPart
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
    activeSeason: activeSeason
    customDomain: customDomain
    adminBootstrapEmail: adminBootstrapEmail
    adminBootstrapPassword: adminBootstrapPassword
    googleOAuthClientId: googleOAuthClientId
    googleOAuthClientSecret: googleOAuthClientSecret
    facebookOAuthAppId: facebookOAuthAppId
    facebookOAuthAppSecret: facebookOAuthAppSecret
    acsConnectionString: enableAcs ? acs!.outputs.connectionString : ''
    acsEmailFromAddress: enableAcs ? acs!.outputs.fromAddress : ''
    acsSmsFromNumber: acsSmsFromNumber
    twilioAccountSid: twilioAccountSid
    twilioAuthToken: twilioAuthToken
    twilioSmsFromNumber: twilioSmsFromNumber
  }
}

// ----- Outputs -----
output appUrl string = 'https://${containerApp.outputs.fqdn}'
output publicBaseUrl string = containerApp.outputs.publicBaseUrl
output defaultFqdn string = containerApp.outputs.defaultFqdn
output containerAppName string = containerApp.outputs.name
output sqlServerFqdn string = sql.outputs.serverFqdn
output managedIdentityClientId string = managedIdentity.outputs.clientId
// Paste these into the Google / Facebook developer consoles when registering the app.
output googleRedirectUri string = containerApp.outputs.googleRedirectUri
output facebookRedirectUri string = containerApp.outputs.facebookRedirectUri
// Needed for the one-time `az containerapp hostname bind` to attach the custom domain.
output customDomainVerificationId string = containerApp.outputs.customDomainVerificationId
