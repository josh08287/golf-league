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

  POST-DEPLOY:
    EF Core migrations run automatically on Function App startup against the
    Azure SQL database. The Function App's managed identity is the AAD admin
    on the SQL server, so it has full DDL privileges. No manual SQL setup
    is required.
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

@description('ADO.NET connection string for the existing Azure SQL database. Must use Authentication=Active Directory Default so the Function App MI is the resolved principal at runtime.')
param sqlConnectionString string

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

// 2. Blob Storage — player photos (database file is no longer used; the
//    container remains in the storage module so existing data isn't deleted).
module storageModule 'modules/storage.bicep' = {
  name: 'storage-deploy'
  params: {
    name: storageAccountName
    location: location
    tags: tags
  }
}

// 3. Azure Functions (deployed first so we can pass its MI principalId to
//    the SQL module as the AAD admin).
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

// 4. Key Vault — grants the Function App Managed Identity the Secrets User role.
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
// Post-module resource: inject final app settings into the Function App
//
// This pattern breaks any circular dependency:
//   - Functions deployed first (step 3) with bootstrap settings only
//   - Key Vault deployed after (step 4)
//   - This appsettings resource runs last and adds DB + KV references
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
    // Connects to the pre-existing Azure SQL DB. Authentication=Active
    // Directory Default resolves to the Function App's system-assigned
    // managed identity at runtime (no secrets, no rotation). The MI must
    // be granted db_owner (or db_datareader+db_datawriter+db_ddladmin) on
    // the target database — see DEPLOY.md.
    SQL_CONNECTION_STRING: sqlConnectionString
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

@description('Name of the storage account.')
output storageAccountName string = storageModule.outputs.storageAccountName

@description('Object (principal) ID of the Function App system-assigned managed identity. Use this when granting the MI access to the SQL database.')
output functionAppPrincipalId string = functionsModule.outputs.principalId
