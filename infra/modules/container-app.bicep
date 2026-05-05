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

@description('Optional custom domain (e.g. registration.lasvegassoccerschool.org). When set, the deploy provisions a managed cert against this hostname (DNS records must already be in place — CNAME to the default Container Apps FQDN, plus TXT asuid.<host>=<verificationId>) and binds it to the ingress. App env vars and OAuth redirect URIs also use this hostname.')
param customDomain string = ''

@description('Min replicas (0 enables scale-to-zero).')
param minReplicas int = 0
param maxReplicas int = 3

var hasGoogle = !empty(googleOAuthClientId) && !empty(googleOAuthClientSecret)
var hasFacebook = !empty(facebookOAuthAppId) && !empty(facebookOAuthAppSecret)
var hasAdminBootstrap = !empty(adminBootstrapEmail) && !empty(adminBootstrapPassword)

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
var allSecrets = concat(baseSecrets, googleSecrets, facebookSecrets, adminSecrets)

var defaultDomain = reference(environmentId, '2024-03-01').defaultDomain
var defaultFqdn = '${name}.${defaultDomain}'
var publicHost = !empty(customDomain) ? customDomain : defaultFqdn
var publicBaseUrl = 'https://${publicHost}'

// Custom-domain binding: managed cert + ingress.customDomains entry. Bicep replaces
// the entire containerApps resource on every deploy, so the binding has to live in
// the template — otherwise each deploy strips out a portal- or CLI-bound domain.
var envName = split(environmentId, '/')[8]
var certName = !empty(customDomain) ? 'mc-${replace(customDomain, '.', '-')}' : ''

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
var allEnv = concat(baseEnv, googleEnv, facebookEnv, adminEnv)

resource managedEnv 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: envName
}

// Managed certificate for the custom domain. Provisioned only when customDomain is set.
// Requires DNS to be in place (CNAME registration → defaultFqdn, TXT asuid.registration → verification ID)
// before this deploy runs, otherwise cert validation fails and the deploy errors out.
resource cert 'Microsoft.App/managedEnvironments/managedCertificates@2024-03-01' = if (!empty(customDomain)) {
  parent: managedEnv
  name: certName
  location: location
  tags: tags
  properties: {
    subjectName: customDomain
    domainControlValidation: 'CNAME'
  }
}

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
        customDomains: empty(customDomain) ? [] : [
          {
            name: customDomain
            bindingType: 'SniEnabled'
            certificateId: cert.id
          }
        ]
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
