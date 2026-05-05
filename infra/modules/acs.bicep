// Azure Communication Services for outreach email + SMS.
// - ACS resource (global) holds the API endpoint and access keys.
// - Email Communication Service hosts the email infrastructure.
// - Azure-managed domain provisions a *.azurecomm.net subdomain instantly
//   (no DNS work). Switch to a customer-managed domain later for prettier
//   sender addresses on lasvegassoccerschool.org.
// - SMS phone numbers are NOT provisioned here — they require regulatory
//   verification and aren't exposed to Bicep. After this deploys, purchase
//   a number in the portal and set the ACS_SMS_FROM_NUMBER repo variable.

@description('Short application name for naming.')
param appName string

@description('Tags applied to all resources.')
param tags object = {}

@description('Region for ACS data (compliance). ACS resources themselves are global.')
param dataLocation string = 'United States'

@description('Local-part of the sender email address. The full address becomes <localPart>@<azure-managed-domain>.azurecomm.net.')
param senderLocalPart string = 'donotreply'

@description('Display name for the sender (shown in email clients).')
param senderDisplayName string = 'Las Vegas Soccer School'

var nameSuffix = uniqueString(resourceGroup().id)

resource emailService 'Microsoft.Communication/emailServices@2023-04-01' = {
  name: '${appName}-email-${nameSuffix}'
  location: 'global'
  tags: tags
  properties: {
    dataLocation: dataLocation
  }
}

// AzureManaged domain auto-provisions a unique <guid>.azurecomm.net subdomain.
// Resource name MUST be 'AzureManagedDomain' for the managed flow.
resource managedDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: emailService
  name: 'AzureManagedDomain'
  location: 'global'
  tags: tags
  properties: {
    domainManagement: 'AzureManaged'
    userEngagementTracking: 'Disabled'
  }
}

resource senderUsername 'Microsoft.Communication/emailServices/domains/senderUsernames@2023-04-01' = {
  parent: managedDomain
  name: senderLocalPart
  properties: {
    username: senderLocalPart
    displayName: senderDisplayName
  }
}

resource acs 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: '${appName}-acs-${nameSuffix}'
  location: 'global'
  tags: tags
  properties: {
    dataLocation: dataLocation
    linkedDomains: [
      managedDomain.id
    ]
  }
}

// Connection string isn't @secure() here because Bicep can't access secure outputs
// from a conditional module via ternary. The container-app param it feeds into is
// @secure() so it lands in a Container App secret at rest.
#disable-next-line outputs-should-not-contain-secrets
output connectionString string = acs.listKeys().primaryConnectionString
output fromAddress string = '${senderLocalPart}@${managedDomain.properties.fromSenderDomain}'
output acsName string = acs.name
output emailServiceName string = emailService.name
