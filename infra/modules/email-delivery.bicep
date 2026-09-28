// Email delivery tracking for Azure Communication Services:
//  - Event Grid sends every EmailDeliveryReportReceived event to a storage queue, which the API
//    drains to mark each broadcast recipient Delivered / Undelivered (bounced, suppressed, ...).
//    A queue (not a webhook) avoids Event Grid's endpoint-validation handshake racing the deploy
//    that ships the endpoint, and it buffers/retries while the app is scaled to zero.
// Needs the Microsoft.EventGrid resource provider registered on the subscription.
//
// ACS diagnostic logs are NOT managed here: the resource already has a portal-created setting
// ('acs-to-loganalytics') sending to the same workspace, and Azure rejects a second setting that
// reuses a sink for the same log category. Add email categories to that setting in the portal.

param acsName string
param storageAccountName string
param emailEventsQueueName string
param tags object = {}

resource acs 'Microsoft.Communication/communicationServices@2023-04-01' existing = {
  name: acsName
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource acsTopic 'Microsoft.EventGrid/systemTopics@2022-06-15' = {
  name: '${acsName}-events'
  location: 'global'
  tags: tags
  properties: {
    source: acs.id
    topicType: 'Microsoft.Communication.CommunicationServices'
  }
}

resource emailDeliveryToQueue 'Microsoft.EventGrid/systemTopics/eventSubscriptions@2022-06-15' = {
  parent: acsTopic
  name: 'email-delivery-to-queue'
  properties: {
    eventDeliverySchema: 'EventGridSchema'
    filter: {
      includedEventTypes: [ 'Microsoft.Communication.EmailDeliveryReportReceived' ]
    }
    destination: {
      endpointType: 'StorageQueue'
      properties: {
        resourceId: storage.id
        queueName: emailEventsQueueName
        // Keep undrained reports a week so a long outage doesn't lose them.
        queueMessageTimeToLiveInSeconds: 604800
      }
    }
    retryPolicy: {
      maxDeliveryAttempts: 30
      eventTimeToLiveInMinutes: 1440
    }
  }
}
