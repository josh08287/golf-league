/*
  Golf League Manager — Main Bicep Deployment
  ============================================

  PREREQUISITES:
    1. Resource group must already exist:
         az group create --name golf-league-prod --location eastus2
    2. Before the first deployment, populate these Key Vault secrets after
       the first apply (the Function App needs them to start cleanly):
         - JwtSigningKey            (>= 32-char random string)
         - AdminBootstrapEmail      (the email of the first admin)
         - GoogleClientId / GoogleClientSecret    (optional, omit to disable)
         - FacebookAppId / FacebookAppSecret      (optional, omit to disable)
       The Function App's Managed Identity has Key Vault Secrets User access,
       so it will pick them up on next restart.

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

@description('ADO.NET connection string for the existing Azure SQL database. Must use Authentication=Active Directory Default so the Function App MI is the resolved principal at runtime.')
param sqlConnectionString string

@description('Public origin(s) of the web client, comma-separated. Used as WebAuthn allowed origins and the default WEB_BASE_URL for invite links.')
param webOrigin string

@description('WebAuthn relying-party ID (the registrable domain, e.g. "app.golfleague.com"). For local dev use "localhost".')
param fido2RpId string = 'localhost'

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

// Key Vault secret reference syntax for app settings. The Function App's
// system-assigned MI resolves these at runtime — the actual secret values
// are stored only in Key Vault, never in app settings.
var kvBaseUri = 'https://${keyVaultName}${az.environment().suffixes.keyvaultDns}/secrets'

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
    BLOB_STORAGE_ACCOUNT: storageModule.outputs.storageAccountName
    WEBSITE_RUN_FROM_PACKAGE: '1'
    SQL_CONNECTION_STRING: sqlConnectionString
    WEB_BASE_URL: webOrigin

    // Local-auth configuration — all sensitive values come from Key Vault.
    // The KV secrets must be created manually after the first apply; see the
    // PREREQUISITES note at the top of this file.
    JWT_SIGNING_KEY: '@Microsoft.KeyVault(SecretUri=${kvBaseUri}/JwtSigningKey)'
    ADMIN_BOOTSTRAP_EMAIL: '@Microsoft.KeyVault(SecretUri=${kvBaseUri}/AdminBootstrapEmail)'
    GOOGLE_CLIENT_ID: '@Microsoft.KeyVault(SecretUri=${kvBaseUri}/GoogleClientId)'
    GOOGLE_CLIENT_SECRET: '@Microsoft.KeyVault(SecretUri=${kvBaseUri}/GoogleClientSecret)'
    FACEBOOK_APP_ID: '@Microsoft.KeyVault(SecretUri=${kvBaseUri}/FacebookAppId)'
    FACEBOOK_APP_SECRET: '@Microsoft.KeyVault(SecretUri=${kvBaseUri}/FacebookAppSecret)'

    // Passkey relying-party config — RP ID is the registrable domain
    // (no scheme, no path). Origins must be the full origin(s) clients
    // connect from. Comma-separated for multiple origins (e.g. web + mobile).
    FIDO2_RP_ID: fido2RpId
    FIDO2_RP_NAME: 'Golf League'
    FIDO2_RP_ORIGINS: webOrigin
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
