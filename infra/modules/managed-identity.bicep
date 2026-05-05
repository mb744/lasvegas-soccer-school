param name string
param location string
param tags object = {}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-07-31-preview' = {
  name: name
  location: location
  tags: tags
}

output id string = identity.id
output name string = identity.name
output principalId string = identity.properties.principalId
output clientId string = identity.properties.clientId
