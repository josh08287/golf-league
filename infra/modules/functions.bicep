// modules/functions.bicep
// Deploys a Consumption-plan Azure Functions app running .NET 9 Isolated Worker.
// The Function App uses a system-assigned Managed Identity for all Azure service
// auth (Key Vault secrets, host storage, blob storage). No connection strings
// are stored in plain text in app settings — Key Vault references are added
// after this module deploys (see main.bicep functionAppSettings resource).

// ---------------------------------------------------------------------------
// Parameters
// ---------------------------------------------------------------------------

@description('Name of the Function App resource.')
param name string

@description('Azure region for all resources in this module.')
param location string

@description('Resource tags to apply.')
param tags object

@description('Six-character unique suffix derived from the resource group ID. Used to name the Functions host storage account.')
param uniqueSuffix string

@description('Application Insights connection string to wire up telemetry.')
param appInsightsConnectionString string

@description('Name of the storage account that holds player photos (for BLOB_STORAGE_ACCOUNT app setting).')
param storageAccountNameForPhotos string

@description('Array of allowed CORS origins (e.g., ["https://app1.com", "https://app2.com"]).')
param allowedOrigins array = []

// ---------------------------------------------------------------------------
// Resources
// ---------------------------------------------------------------------------

// Dedicated storage account for the Functions runtime host (separate from the
// photos storage account so lifecycle policies and access tiers don't conflict).
resource functionsHostStorage 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: 'fnstore${uniqueSuffix}'
  location: location
  kind: 'StorageV2'
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowSharedKeyAccess: true
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

// Consumption (Y1 / Dynamic) plan — pay per execution, zero cost when idle
resource hostingPlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: '${name}-plan'
  location: location
  kind: 'functionapp'
  tags: tags
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {
    reserved: false
  }
}

// Function App — .NET 9 Isolated Worker with system-assigned Managed Identity.
// Only minimal bootstrap settings are set here; the full set (including Key
// Vault references) is patched in by main.bicep after Key Vault deploys.
resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: name
  location: location
  kind: 'functionapp'
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      cors: {
        allowedOrigins: allowedOrigins
        supportCredentials: true
      }
      appSettings: [
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        // Use Managed Identity to auth against host storage — no connection string needed
        {
          name: 'AzureWebJobsStorage__accountName'
          value: functionsHostStorage.name
        }
        {
          name: 'BLOB_STORAGE_ACCOUNT'
          value: storageAccountNameForPhotos
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
      ]
    }
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------

@description('Name of the deployed Function App.')
output functionAppName string = functionApp.name

@description('Default hostname of the Function App (without https:// prefix).')
output functionAppUrl string = functionApp.properties.defaultHostName

@description('Object (principal) ID of the Function App system-assigned Managed Identity.')
output principalId string = functionApp.identity.principalId

@description('Principal type of the identity — always ServicePrincipal for system-assigned MI.')
output principalType string = 'ServicePrincipal'
