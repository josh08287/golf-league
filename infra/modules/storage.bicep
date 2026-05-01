// modules/storage.bicep
// Deploys a storage account for player photos with the following characteristics:
//   - Cool access tier (infrequently accessed binary blobs)
//   - Soft delete for blobs (7-day retention)
//   - No public blob access
//   - TLS 1.2 minimum
//   - Lifecycle management rule to archive blobs after 90 days

// ---------------------------------------------------------------------------
// Parameters
// ---------------------------------------------------------------------------

@description('Name of the storage account. Must be globally unique, lowercase, no hyphens, max 24 chars.')
param name string

@description('Azure region for this resource.')
param location string

@description('Resource tags to apply.')
param tags object

// ---------------------------------------------------------------------------
// Resources
// ---------------------------------------------------------------------------

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: name
  location: location
  kind: 'StorageV2'
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Cool'
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowSharedKeyAccess: true
    defaultToOAuthAuthentication: false
    encryption: {
      services: {
        blob: {
          enabled: true
        }
        file: {
          enabled: true
        }
      }
      keySource: 'Microsoft.Storage'
    }
  }
}

// Enable blob soft delete (7-day retention) to protect against accidental deletion
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: 7
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 7
    }
  }
}

// Container for player profile photos — no public access
resource playerPhotosContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'player-photos'
  properties: {
    publicAccess: 'None'
  }
}

// Container for the SQLite database file — no public access
resource databaseContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'database'
  properties: {
    publicAccess: 'None'
  }
}

// Lifecycle management: tier blobs to Archive after 90 days to minimize storage cost
resource lifecyclePolicy 'Microsoft.Storage/storageAccounts/managementPolicies@2023-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    policy: {
      rules: [
        {
          name: 'ArchivePlayerPhotosAfter90Days'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: [
                'blockBlob'
              ]
              prefixMatch: [
                'player-photos/'
              ]
            }
            actions: {
              baseBlob: {
                tierToArchive: {
                  daysAfterModificationGreaterThan: 90
                }
              }
            }
          }
        }
      ]
    }
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------

@description('Name of the storage account.')
output storageAccountName string = storageAccount.name

@description('Primary blob endpoint URI (e.g. https://glfstrabc123.blob.core.windows.net/).')
output blobEndpoint string = storageAccount.properties.primaryEndpoints.blob

