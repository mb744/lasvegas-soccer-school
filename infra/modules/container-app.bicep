param name string
param location string
param tags object = {}
param environmentId string
param image string
param managedIdentityResourceId string
param managedIdentityClientId string

@secure()
param sqlConnectionString string

@description('Email of the bootstrap admin user. If set, the API ensures this user exists with the Admin role on startup.')
param adminBootstrapEmail string = ''

@secure()
@description('Initial password for the bootstrap admin user. Used only when creating; ignored if the user already exists.')
param adminBootstrapPassword string = ''

@description('Google OAuth Client ID. Empty disables Google login.')
param googleOAuthClientId string = ''

@secure()
param googleOAuthClientSecret string = ''

@description('Facebook OAuth App ID. Empty disables Facebook login.')
param facebookOAuthAppId string = ''

@secure()
param facebookOAuthAppSecret string = ''

@description('Active season label, e.g. "2026/27". Stamped onto new registrations.')
param activeSeason string = '2026/27'

@description('Optional ACS connection string for outreach email/SMS. Empty disables ACS.')
@secure()
param acsConnectionString string = ''

@description('Optional sender email for ACS, e.g. donotreply@xxx.azurecomm.net. Empty disables email outreach.')
param acsEmailFromAddress string = ''

@description('Optional sender phone number for ACS SMS in E.164 format, e.g. +18005551212. Empty disables SMS outreach.')
param acsSmsFromNumber string = ''

@description('Optional Twilio Account SID. When all three Twilio params are set, OutreachSender uses Twilio for SMS instead of ACS.')
param twilioAccountSid string = ''

@secure()
@description('Twilio auth token (aka API secret).')
param twilioAuthToken string = ''

@description('Twilio sender phone number in E.164 format (e.g. +18005551212). Must be a Twilio-owned, fully verified number.')
param twilioSmsFromNumber string = ''

@description('Twilio WhatsApp-enabled sender in E.164 (no "whatsapp:" prefix, e.g. +18005551212). Required for the WhatsApp channel in the admin messaging feature; empty disables WhatsApp.')
param twilioWhatsAppFromNumber string = ''

@description('Optional approved WhatsApp template Content SID (HX...) for business-initiated WhatsApp messages outside the 24h customer-service window. Empty = free-form only.')
param twilioWhatsAppTemplateSid string = ''

@description('Optional Twilio Conversations Service SID (IS...) for true group chat. Empty uses the account default service.')
param twilioConversationsServiceSid string = ''

@description('Optional custom domain (e.g. registration.lasvegassoccerschool.org). When set, PublicBaseUrl + OAuth redirect URIs use it instead of the auto-generated Container Apps FQDN. The actual hostname binding (managed cert + ingress.customDomains) is done by a post-Bicep step in deploy.yml because cert provisioning requires the hostname to be already registered on the container app, which Bicep cannot do in a single pass.')
param customDomain string = ''

@description('Optional bare/apex domain (e.g. lasvegassoccerschool.org). When set, App middleware 301-redirects requests hitting the apex to https://registration.<apex>, so TFN reviewers and humans typing the bare domain land on the real site instead of a parking page. Requires DNS A record at apex + post-Bicep `az containerapp hostname add/bind` for the apex.')
param apexDomain string = ''

@description('Min replicas (0 enables scale-to-zero).')
param minReplicas int = 0
param maxReplicas int = 3

var hasGoogle = !empty(googleOAuthClientId) && !empty(googleOAuthClientSecret)
var hasFacebook = !empty(facebookOAuthAppId) && !empty(facebookOAuthAppSecret)
var hasAdminBootstrap = !empty(adminBootstrapEmail) && !empty(adminBootstrapPassword)
var hasAcs = !empty(acsConnectionString)
var hasTwilio = !empty(twilioAccountSid) && !empty(twilioAuthToken) && !empty(twilioSmsFromNumber)

var baseSecrets = [
  { name: 'sql-connection-string', value: sqlConnectionString }
]
var googleSecrets = hasGoogle ? [
  { name: 'google-oauth-secret', value: googleOAuthClientSecret }
] : []
var facebookSecrets = hasFacebook ? [
  { name: 'facebook-oauth-secret', value: facebookOAuthAppSecret }
] : []
var adminSecrets = hasAdminBootstrap ? [
  { name: 'admin-bootstrap-password', value: adminBootstrapPassword }
] : []
var acsSecrets = hasAcs ? [
  { name: 'acs-connection-string', value: acsConnectionString }
] : []
var twilioSecrets = hasTwilio ? [
  { name: 'twilio-auth-token', value: twilioAuthToken }
] : []
var allSecrets = concat(baseSecrets, googleSecrets, facebookSecrets, adminSecrets, acsSecrets, twilioSecrets)

var defaultDomain = reference(environmentId, '2024-03-01').defaultDomain
var defaultFqdn = '${name}.${defaultDomain}'
var publicHost = !empty(customDomain) ? customDomain : defaultFqdn
var publicBaseUrl = 'https://${publicHost}'

var baseEnv = [
  { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
  { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
  // Tells DefaultAzureCredential which user-assigned managed identity to use.
  { name: 'AZURE_CLIENT_ID', value: managedIdentityClientId }
  { name: 'ConnectionStrings__DefaultConnection', secretRef: 'sql-connection-string' }
  { name: 'App__ActiveSeason', value: activeSeason }
  { name: 'App__PublicBaseUrl', value: publicBaseUrl }
  // CORS not strictly needed in single-origin deploy but kept for parity with dev.
  { name: 'App__Cors__AllowedOrigins__0', value: publicBaseUrl }
]
var apexEnv = !empty(apexDomain) ? [
  { name: 'App__ApexDomain', value: apexDomain }
] : []
var googleEnv = hasGoogle ? [
  { name: 'App__OAuth__Google__ClientId', value: googleOAuthClientId }
  { name: 'App__OAuth__Google__ClientSecret', secretRef: 'google-oauth-secret' }
] : []
var facebookEnv = hasFacebook ? [
  { name: 'App__OAuth__Facebook__AppId', value: facebookOAuthAppId }
  { name: 'App__OAuth__Facebook__AppSecret', secretRef: 'facebook-oauth-secret' }
] : []
var adminEnv = hasAdminBootstrap ? [
  { name: 'App__Admin__Email', value: adminBootstrapEmail }
  { name: 'App__Admin__Password', secretRef: 'admin-bootstrap-password' }
] : []
// ACS: connection string is required for any sending; email/sms from values are
// optional individually, so only the configured channels appear in the env.
var acsEnvCore = hasAcs ? [
  { name: 'Acs__ConnectionString', secretRef: 'acs-connection-string' }
] : []
var acsEnvEmail = hasAcs && !empty(acsEmailFromAddress) ? [
  { name: 'Acs__EmailFromAddress', value: acsEmailFromAddress }
] : []
var acsEnvSms = hasAcs && !empty(acsSmsFromNumber) ? [
  { name: 'Acs__SmsFromNumber', value: acsSmsFromNumber }
] : []
var twilioEnv = hasTwilio ? [
  { name: 'Twilio__AccountSid', value: twilioAccountSid }
  { name: 'Twilio__AuthToken', secretRef: 'twilio-auth-token' }
  { name: 'Twilio__SmsFromNumber', value: twilioSmsFromNumber }
] : []
// WhatsApp + Conversations are independently optional once Twilio creds exist, so they
// each get their own conditional block instead of being lumped into the SMS gate.
var twilioWhatsAppEnv = hasTwilio && !empty(twilioWhatsAppFromNumber) ? [
  { name: 'Twilio__WhatsAppFromNumber', value: twilioWhatsAppFromNumber }
] : []
var twilioWhatsAppTemplateEnv = hasTwilio && !empty(twilioWhatsAppTemplateSid) ? [
  { name: 'Twilio__WhatsAppTemplateSid', value: twilioWhatsAppTemplateSid }
] : []
var twilioConversationsEnv = hasTwilio && !empty(twilioConversationsServiceSid) ? [
  { name: 'Twilio__ConversationsServiceSid', value: twilioConversationsServiceSid }
] : []
var allEnv = concat(baseEnv, apexEnv, googleEnv, facebookEnv, adminEnv, acsEnvCore, acsEnvEmail, acsEnvSms, twilioEnv, twilioWhatsAppEnv, twilioWhatsAppTemplateEnv, twilioConversationsEnv)

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityResourceId}': {}
    }
  }
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
        traffic: [
          { latestRevision: true, weight: 100 }
        ]
        // customDomains intentionally not set here. A post-Bicep step in
        // deploy.yml runs `az containerapp hostname add/bind` after the
        // container app exists, which Azure handles in the right order
        // (registers hostname first, then provisions the managed cert).
      }
      secrets: allSecrets
      // GHCR public package — no registry auth needed.
      // To switch to a private package, add a `registries` block referencing a PAT secret.
    }
    template: {
      containers: [
        {
          name: 'app'
          image: image
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: allEnv
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/health', port: 8080 }
              initialDelaySeconds: 10
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: { path: '/health', port: 8080 }
              initialDelaySeconds: 5
              periodSeconds: 10
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http-rule'
            http: {
              metadata: { concurrentRequests: '50' }
            }
          }
        ]
      }
    }
  }
}

output id string = app.id
output name string = app.name
output fqdn string = app.properties.configuration.ingress.fqdn
output defaultFqdn string = defaultFqdn
output publicBaseUrl string = publicBaseUrl
output googleRedirectUri string = '${publicBaseUrl}/signin-google'
output facebookRedirectUri string = '${publicBaseUrl}/signin-facebook'
// Needed for the one-time `az containerapp hostname add/bind` after DNS records are placed.
output customDomainVerificationId string = app.properties.customDomainVerificationId
