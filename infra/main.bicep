/*
  Golf League Manager — Main Bicep Deployment
  ============================================

  PREREQUISITES:
    1. Resource group must already exist:
         az group create --name golf-league-prod --location eastus2
    2. Entra External ID tenant must be created manually in the Azure portal
       before deployment. The tenant ID and API client ID are supplied
       as parameters; Bicep only references them — it does not create them.

  DEPLOY COMMAND:
    az deployment group create \
      --resource-group golf-league-prod \
      --template-file main.bicep \
      --parameters prod.parameters.json

  POST-DEPLOY STEPS:
    1. Download the SQLite database file from blob storage (initially empty),
       run EF Core migrations locally, then re-upload:
         az storage blob download \
           --account-name <storageAccountName> \
           --container-name database \
           --name golf-league.db \
           --file golf-league.db \
           --auth-mode login
         dotnet ef database update \
           --project src/GolfLeague.Infrastructure \
           --startup-project src/GolfLeague.Functions \
           --connection "Data Source=golf-league.db"
         az storage blob upload \
           --account-name <storageAccountName> \
           --container-name database \
           --name golf-league.db \
           --file golf-league.db \
           --auth-mode login
*/

targetScope = 'resourceGroup'

// ---------------------------------------------------------------------------
// Parameters
// ---------------------------------------------------------------------------

@description('Azure region for all resources.')
param location string

@description('Environment name (e.g. prod). Used in tags and resource names.')
param environmentName string

@description('Base application name used to construct resource names.')
param appName string

@description('Entra External ID tenant ID (GUID from the Overview blade of your External ID tenant). Create the External ID tenant manually before deploying.')
param entraExternalTenantId string

@description('Entra External ID application (client) ID registered for the API. Create this app registration in the External ID tenant before deploying.')
param entraClientId string

// ---------------------------------------------------------------------------
// Variables
// ---------------------------------------------------------------------------

var uniqueSuffix = take(uniqueString(resourceGroup().id), 6)

var tags = {
  application: 'golf-league'
  environment: environmentName
  managedBy: 'bicep'
}

var appInsightsName    = '${appName}-ai-${uniqueSuffix}'
var storageAccountName = 'glfstr${uniqueSuffix}'
var functionAppName    = '${appName}-fn-${uniqueSuffix}'
var keyVaultName       = '${appName}-kv-${uniqueSuffix}'

// ---------------------------------------------------------------------------
// Modules
// ---------------------------------------------------------------------------

// 1. Application Insights (Log Analytics workspace + AI component)
module appInsightsModule 'modules/appinsights.bicep' = {
  name: 'appinsights-deploy'
  params: {
    name: appInsightsName
    location: location
    tags: tags
  }
}

// 2. Blob Storage — player photos + SQLite database file
module storageModule 'modules/storage.bicep' = {
  name: 'storage-deploy'
  params: {
    name: storageAccountName
    location: location
    tags: tags
  }
}

// 3. Azure Functions (deploys without Key Vault references to avoid circular dependency)
module functionsModule 'modules/functions.bicep' = {
  name: 'functions-deploy'
  params: {
    name: functionAppName
    location: location
    tags: tags
    uniqueSuffix: uniqueSuffix
    appInsightsConnectionString: appInsightsModule.outputs.connectionString
    storageAccountNameForPhotos: storageModule.outputs.storageAccountName
    entraExternalTenantId: entraExternalTenantId
    entraClientId: entraClientId
  }
}

// 4. Key Vault — grants the Function App Managed Identity the Secrets User role
module keyVaultModule 'modules/keyvault.bicep' = {
  name: 'keyvault-deploy'
  params: {
    name: keyVaultName
    location: location
    tags: tags
    functionAppPrincipalId: functionsModule.outputs.principalId
  }
}

// ---------------------------------------------------------------------------
// Post-module resource: inject Key Vault reference into Function App settings
//
// This pattern breaks the circular dependency:
//   - Functions deployed first (step 3) with plain app settings, no KV refs
//   - Key Vault deployed second (step 4) and grants access to the Function MI
//   - This appsettings resource runs last and adds the KV reference
// ---------------------------------------------------------------------------

resource functionAppSettings 'Microsoft.Web/sites/config@2023-01-01' = {
  name: '${functionAppName}/appsettings'
  properties: {
    FUNCTIONS_EXTENSION_VERSION: '~4'
    FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
    APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsModule.outputs.connectionString
    AzureWebJobsStorage__accountName: 'fnstore${uniqueSuffix}'
    ENTRA_TENANT_ID: entraExternalTenantId
    ENTRA_CLIENT_ID: entraClientId
    BLOB_STORAGE_ACCOUNT: storageModule.outputs.storageAccountName
    WEBSITE_RUN_FROM_PACKAGE: '1'
    // SQLite database file is downloaded from blob storage at function startup
    SQLITE_BLOB_CONTAINER: 'database'
    SQLITE_BLOB_NAME: 'golf-league.db'
  }
  dependsOn: [
    keyVaultModule
  ]
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------

@description('HTTPS URL of the deployed Function App.')
output functionAppUrl string = 'https://${functionsModule.outputs.functionAppUrl}'

@description('Name of the deployed Function App.')
output functionAppName string = functionsModule.outputs.functionAppName

@description('Name of the deployed Key Vault.')
output keyVaultName string = keyVaultModule.outputs.name

@description('Name of the storage account (needed for post-deploy migration steps).')
output storageAccountName string = storageModule.outputs.storageAccountName
